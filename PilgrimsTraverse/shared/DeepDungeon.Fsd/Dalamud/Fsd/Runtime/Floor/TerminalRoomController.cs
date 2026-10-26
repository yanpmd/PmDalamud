using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DeepDungeon.Fsd.Core;
using DeepDungeon.Fsd.Dalamud.Runtime.Navigation;
using global::Dalamud.Game.ClientState.Conditions;
using global::Dalamud.Game.ClientState.Objects.Enums;
using global::Dalamud.Game.ClientState.Objects.Types;
using Treasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;

namespace DeepDungeon.Fsd.Dalamud.Runtime.Floor;

internal sealed partial class TerminalRoomController
{
    private RunContext? _ctx;
    private NavigationHelper? _navHelper;
    private readonly Action<string> _setStatus;
    private readonly Action<string, object> RecordReplayEvent;
    private readonly Action<byte, string> DestroyFloorRuntime;
    private readonly Func<Vector3, Vector3> ResolvePassageWalkingPosition;
    private string Status { set => _setStatus(value); }

    public TerminalRoomController(Action<string> setStatus, Action<string, object> record,
        Action<byte, string> endFloor, Func<Vector3, Vector3> walkingPosition)
    {
        _setStatus = setStatus;
        RecordReplayEvent = record;
        DestroyFloorRuntime = endFloor;
        ResolvePassageWalkingPosition = walkingPosition;
    }

    public void Initialize(RunContext context, NavigationHelper navigation)
    {
        ResetResultRoom();
        _ctx = context;
        _navHelper = navigation;
    }

    private readonly HashSet<ulong> _resultOpened = new();
    private IGameObject? _resultChest;
    private DateTime _resultNextTick, _resultStarted, _resultChestStarted, _resultNextInteract, _resultInteractedAt;
    private bool _resultEntered;
    private ulong _resultAltarInteractedId;
    private long _resultAltarCompletionBefore;
    private bool _resultAltarComplete;

    // PT100 uses its observed altar event; native completion at 99 cannot skip it.
    // The explicit diagnostic boundary still retains the room before interaction.
    public unsafe void UpdateResultRoom()
    {
        var context = _ctx;
        var player = Service.LocalPlayer;
        if (context == null || player == null) return;
        var now = DateTime.UtcNow;
        if (now < _resultNextTick) return;
        _resultNextTick = now.AddMilliseconds(500);
        if (!_resultEntered)
        {
            DestroyFloorRuntime(context.Duty.Floor, "result-room");
            _resultEntered = true;
            context.TerminalRoomObserved = true;
            _resultStarted = now;
            RecordReplayEvent("result-room-entered", new
            {
                floor = context.Duty.Floor, dungeonId = context.Duty.DungeonId,
                player = new { player.Position.X, player.Position.Y, player.Position.Z },
                objects = Service.GameObjects.Select(obj => new
                {
                    obj.GameObjectId, obj.BaseId, kind = obj.ObjectKind.ToString(),
                    obj.IsTargetable, name = obj.Name.TextValue,
                    position = new { obj.Position.X, obj.Position.Y, obj.Position.Z }
                }).ToArray()
            });
            Service.Log.Info("[ResultRoom] Native result floor entered; random-room graph, combat and passage disabled.");
        }
        if (context.FarmingPlan?.StopsForTerminalReview == true)
        {
            _navHelper?.Cancel();
            context.Navigator.CancelAll();
            context.TerminalReviewRequested = true;
            context.StatusLine = Status = "已到达100层；停止并保留终局房间，等待地图／奖励物件验收。";
            RecordReplayEvent("result-room-diagnostic-stop", new { floor = context.Duty.Floor, opened = _resultOpened.Count });
            Service.Log.Info("[ResultRoom] Diagnostic boundary reached: floor=100; retained room before reward navigation/interaction.");
            return;
        }
        if (Service.Condition[ConditionFlag.InCombat] || player.IsCasting) return;

        if (context.Duty.DungeonId == 4 && context.Duty.Floor == 100)
        {
            UpdatePt100Altar(now);
            return;
        }

        // ObjectKind.Treasure is authoritative independently of translated names.
        // Event-object reward coffers need their first live identity/state capture.
        if (_resultChest != null)
        {
            var live = Service.GameObjects.FirstOrDefault(obj => obj.GameObjectId == _resultChest.GameObjectId &&
                obj.EntityId == _resultChest.EntityId && obj.BaseId == _resultChest.BaseId);
            if (live == null)
            {
                StopResultReview("奖励箱在确认原生开启状态前消失；请验收100层物件与奖励。", now);
                return;
            }
            var treasure = (Treasure*)live.Address;
            if (_resultInteractedAt != default &&
                treasure->State >= Treasure.TreasureState.Opened)
            {
                _resultOpened.Add(live.GameObjectId);
                RecordReplayEvent("result-chest-opened", new { live.GameObjectId, live.BaseId,
                    state = (byte)treasure->State, flags = (byte)treasure->Flags });
                Service.Log.Info($"[ResultRoom] Reward coffer opened id={live.GameObjectId}, baseId={live.BaseId}, state={treasure->State}; inventory receipt/exit still require live verification");
                _resultChest = null;
                _resultInteractedAt = default;
                _resultStarted = now; // Allow subsequently spawned coffers a full quiet window.
            }
            else if ((now - (_resultInteractedAt == default ? _resultChestStarted : _resultInteractedAt)).TotalSeconds > 60)
            {
                StopResultReview("奖励箱导航／开启逾时；已保留100层现场。", now);
                return;
            }
        }
        if (_resultChest == null)
        {
            float nearest = float.MaxValue;
            foreach (var obj in Service.GameObjects)
            {
                if (obj.ObjectKind != ObjectKind.Treasure || !obj.IsTargetable ||
                    _resultOpened.Contains(obj.GameObjectId)) continue;
                var treasure = (Treasure*)obj.Address;
                if (treasure->State != Treasure.TreasureState.Unopened) continue;
                float distance = Vector3.DistanceSquared(player.Position, obj.Position);
                if (distance < nearest) { nearest = distance; _resultChest = obj; }
            }
            if (_resultChest == null)
            {
                Status = "100层：等待奖励箱物件";
                if ((now - _resultStarted).TotalSeconds >= 10)
                    StopResultReview($"100层已确认开启 {_resultOpened.Count} 个奖励箱；请验收奖励与出口，FSD 已停止并保留现场。", now);
                return;
            }
            _resultChestStarted = now;
        }
        var navigation = _navHelper!.Navigate(_resultChest.Position, player.Position, 2.5f);
        Status = $"100层：前往奖励箱 {_resultChest.BaseId}";
        if (navigation is NavigationState.Failed or NavigationState.StuckGiveUp)
        {
            StopResultReview("奖励房导航失败；请验收100层地图。", now);
            return;
        }
        if (navigation != NavigationState.Arrived || now < _resultNextInteract) return;
        _resultNextInteract = now.AddSeconds(3);
        GameInteraction.InteractWith(_resultChest, 3f, false, out var failure, out bool dispatched);
        if (dispatched && _resultInteractedAt == default) _resultInteractedAt = now;
        RecordReplayEvent("result-chest-interaction", new { _resultChest.GameObjectId,
            _resultChest.BaseId, dispatched, failure });
    }

    private void ResetResultRoom()
    {
        _resultOpened.Clear();
        _resultChest = null;
        _resultEntered = false;
        _resultAltarInteractedId = 0;
        _resultAltarCompletionBefore = 0;
        _resultAltarComplete = false;
        _resultNextTick = _resultStarted = _resultChestStarted = _resultNextInteract = _resultInteractedAt = default;
    }

    private void UpdatePt100Altar(DateTime now)
    {
        var context = _ctx!;
        var player = Service.LocalPlayer;
        if (player == null || _resultAltarComplete) return;
        var altar = Service.GameObjects.FirstOrDefault(obj => obj.ObjectKind == ObjectKind.EventObj &&
            obj.BaseId == Pt100AltarPolicy.AltarBaseId);
        if (altar != null && Pt100AltarPolicy.CanConfirm(_resultAltarInteractedId, altar.GameObjectId,
            altar.IsTargetable, _resultAltarCompletionBefore, context.DutyCompletionSequence, context.DutyFailureObserved))
        {
            _resultAltarComplete = true;
            _navHelper?.Cancel();
            context.Navigator.CancelAll();
            context.TerminalRewardsRequired = false;
            context.StatusLine = Status = "100层祭坛互动完成；正常退本。";
            RecordReplayEvent("result-altar-completed", new
            {
                altar.GameObjectId, altar.BaseId, before = _resultAltarCompletionBefore,
                after = context.DutyCompletionSequence
            });
            Service.Log.Info("[ResultRoom] PT100 altar state transition and fresh duty completion confirmed; normal leave enabled, no exit-object interaction required.");
            return;
        }
        if ((now - _resultStarted).TotalSeconds > 60)
        {
            StopResultReview("100层祭坛导航／完成确认逾时；已保留现场。", now);
            return;
        }
        if (altar == null || !altar.IsTargetable)
        {
            Status = "100层：等待祭坛物件／完成事件";
            return;
        }
        if (_resultAltarInteractedId != 0 && altar.GameObjectId != _resultAltarInteractedId)
        {
            StopResultReview("100层祭坛身份在互动后改变；已保留现场。", now);
            return;
        }
        if (Service.Condition[ConditionFlag.OccupiedInEvent] ||
            Service.Condition[ConditionFlag.OccupiedInQuestEvent] ||
            Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return;
        var navigation = _navHelper!.Navigate(ResolvePassageWalkingPosition(altar.Position), player.Position, 2.5f);
        context.StatusLine = Status = "100层：前往小型祭坛";
        if (navigation is NavigationState.Failed or NavigationState.StuckGiveUp)
        {
            StopResultReview("100层祭坛导航失败；已保留现场。", now);
            return;
        }
        if (navigation != NavigationState.Arrived || now < _resultNextInteract) return;
        _resultNextInteract = now.AddSeconds(3);
        long completionBefore = context.DutyCompletionSequence;
        GameInteraction.InteractWith(altar, 3f, false, out var failure, out bool dispatched);
        if (dispatched)
        {
            if (_resultAltarInteractedId == 0)
            {
                _resultAltarInteractedId = altar.GameObjectId;
                _resultAltarCompletionBefore = completionBefore;
            }
            _navHelper.Cancel();
        }
        RecordReplayEvent("result-altar-interaction", new { altar.GameObjectId, altar.BaseId,
            dispatched, failure, completionBefore });
        Service.Log.Info($"[ResultRoom] PT100 altar interaction: id={altar.GameObjectId}, dispatched={dispatched}, reason={failure}; awaiting altar state and native completion.");
    }

    private void StopResultReview(string message, DateTime now)
    {
        _navHelper?.Cancel();
        _ctx!.StatusLine = Status = message;
        _ctx.StatusIsError = true;
        RecordReplayEvent("result-room-review-required", new
        {
            message, at = now, opened = _resultOpened.ToArray(),
            objects = Service.GameObjects.Select(obj => new
            { obj.GameObjectId, obj.BaseId, kind = obj.ObjectKind.ToString(), obj.IsTargetable,
                name = obj.Name.TextValue,
                position = new { obj.Position.X, obj.Position.Y, obj.Position.Z } }).ToArray()
        });
        Service.Log.Info("[ResultRoom] " + message);
    }
}
