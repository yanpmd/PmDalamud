using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Mappy.Controllers;

/// <summary>
/// Thin wrapper around the vnavmesh plugin.
/// Everything is optional, all entry points degrade gracefully when vnavmesh is missing or its navmesh is not loaded yet.
/// </summary>
public static class VnavController
{
    /// <summary>
    /// Vertical search radii used when navigating, the height of a marker is frequently just a guess.
    /// </summary>
    private static readonly float[] VerticalSearchSteps = [5.0f, 50.0f, 200.0f, 500.0f];

    /// <summary>
    /// General action that summons a random unlocked mount, the game picks one the player can actually use.
    /// </summary>
    private const uint MountRouletteAction = 9;

    /// <summary>
    /// A mount takes a moment to appear, waiting longer than this means it failed and walking is the only option.
    /// </summary>
    private static readonly TimeSpan MountTimeout = TimeSpan.FromSeconds(5.0);

    private static ICallGateSubscriber<bool>? isReady;
    private static ICallGateSubscriber<Vector3, float, float, Vector3?>? nearestPoint;
    private static ICallGateSubscriber<object>? stop;
    private static ICallGateSubscriber<Vector3, bool, bool>? pathfindAndMoveTo;

    private static Vector3? pendingTarget;
    private static bool pendingFly;
    private static DateTime mountRequestedAt;

    private static void EnsureInitialized()
    {
        isReady ??= Service.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        nearestPoint ??= Service.PluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        stop ??= Service.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        pathfindAndMoveTo ??= Service.PluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
    }

    /// <summary>
    /// Abort any running pathfinding, used when something else takes over the movement (a teleport for example),
    /// otherwise the character keeps walking to the old destination afterwards.
    /// </summary>
    public static void Stop()
    {
        pendingTarget = null;

        try
        {
            EnsureInitialized();
            stop!.InvokeAction();
        }
        catch
        {
            // vnavmesh not installed, nothing is moving anyway.
        }
    }

    public static bool IsReady()
    {
        try
        {
            EnsureInitialized();
            return isReady!.InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Snap a position onto the navigation mesh, returns the input when vnavmesh is unavailable or nothing was found nearby.
    /// </summary>
    public static Vector3 SnapToNavmesh(Vector3 position, float horizontalSearch = 5.0f, float verticalSearch = 5.0f)
        => TrySnap(position, horizontalSearch, verticalSearch) ?? position;

    /// <summary>
    /// Snap the position onto the navmesh and hand it to vnavmesh through its text command.
    /// A long trip starts with mounting, a flying path obviously needs one as well.
    /// </summary>
    public static bool NavigateTo(Vector3 position, bool fly)
    {
        if (!IsReady())
        {
            Service.Log.Warning("[Vnav] navmesh not ready, navigation skipped");
            Service.ChatGui.Print($"[Mappy] {Localization.Get("Vnav.NotReady", "vnavmesh is not ready, can not navigate.")}");

            return false;
        }

        var mounted = Service.Condition[ConditionFlag.Mounted];
        if (mounted) return IssueMove(position, fly);

        // Mounting is disabled by the user, a flying path is not possible without one either.
        if (!System.SystemConfig.AutoMount) return IssueMove(position, fly: false);

        // Mounting is impossible here, walking is the only thing that can work.
        if (!CanMount())
        {
            if (fly) Service.Log.Information($"[Vnav] can not mount in territory {Service.ClientState.TerritoryType}, walking instead");

            return IssueMove(position, fly: false);
        }

        var distance = Service.ObjectTable.LocalPlayer is { } player ? Vector3.Distance(player.Position, position) : 0.0f;

        // Only worth a mount when the destination is far away, and flying needs one no matter the distance.
        if (distance <= System.SystemConfig.AutoMountDistance && !fly) return IssueMove(position, fly: false);

        Service.Log.Information($"[Vnav] target is {distance:F0}y away, mounting before {(fly ? "flying" : "walking")}");

        if (!TrySummonMount())
        {
            // The game refused, waiting for a mount that will never come just wastes five seconds.
            Service.Log.Warning($"[Vnav] no mount available here, walking to {position} on foot");

            return IssueMove(position, fly: false);
        }

        pendingTarget = position;
        pendingFly = fly;
        mountRequestedAt = DateTime.UtcNow;

        return false;
    }

    /// <summary>
    /// Called every frame, issues the deferred path once the player actually sits on a mount.
    /// </summary>
    public static void Update()
    {
        if (pendingTarget is not { } target) return;

        if (DateTime.UtcNow - mountRequestedAt > MountTimeout)
        {
            Service.Log.Warning("[Vnav] mounting timed out, walking instead");

            pendingTarget = null;
            IssueMove(target, fly: false);

            return;
        }

        // The summon takes a moment, keep waiting.
        if (!Service.Condition[ConditionFlag.Mounted]) return;

        pendingTarget = null;

        Service.Log.Information($"[Vnav] mounted, issuing {(pendingFly ? "flyto" : "moveto")} to {target}");
        IssueMove(target, pendingFly);
    }

    /// <summary>
    /// Whether the current territory allows mounting at all, there is no point requesting a mount in an area that
    /// forbids it, the click would just hang until the timeout.
    /// </summary>
    private static bool CanMount()
    {
        if (Service.Condition[ConditionFlag.InCombat]) return false;
        if (Service.Condition[ConditionFlag.Diving]) return false;
        if (Service.Condition[ConditionFlag.Swimming]) return false;

        var territory = Service.DataManager.GetExcelSheet<TerritoryType>().GetRow(Service.ClientState.TerritoryType);

        return territory.RowId is not 0 && territory.Mount;
    }

    /// <summary>
    /// Summon a mount through the action manager, the plain "/mount" text command is not picked up reliably.
    /// Falls back to the chat command so the game can pick whatever mount the player has available.
    /// </summary>
    private static unsafe bool TrySummonMount()
    {
        var actionManager = ActionManager.Instance();
        if (actionManager is null) return false;

        var preferred = System.SystemConfig.MountId;
        var playerState = PlayerState.Instance();

        // The configured mount, when the player actually owns it.
        if (preferred is not 0 && playerState is not null && playerState->IsMountUnlocked((ushort)preferred))
        {
            var status = actionManager->GetActionStatus(ActionType.Mount, preferred);

            if (status is 0 && actionManager->UseAction(ActionType.Mount, preferred))
            {
                Service.Log.Information($"[Vnav] summoned the preferred mount {preferred}");

                return true;
            }

            Service.Log.Information($"[Vnav] preferred mount {preferred} not usable, action status {status}");
        }

        // Let the game pick any suitable mount, this also sidesteps per mount restrictions.
        if (actionManager->GetActionStatus(ActionType.GeneralAction, MountRouletteAction) == 0 &&
            actionManager->UseAction(ActionType.GeneralAction, MountRouletteAction))
        {
            Service.Log.Information($"[Vnav] summoned a mount through the roulette ({MountRouletteAction})");

            return true;
        }

        return false;
    }

    private static bool IssueMove(Vector3 position, bool fly)
    {
        var playerPosition = Service.ObjectTable.LocalPlayer?.Position;
        var target = SnapForNavigation(position);
        var distance = playerPosition is { } player ? Vector3.Distance(player, target) : -1.0f;

        var issued = false;

        try
        {
            EnsureInitialized();
            issued = pathfindAndMoveTo!.InvokeFunc(target, fly);
        }
        catch (Exception exception)
        {
            Service.Log.Warning(exception, "[Vnav] pathfind call failed");
        }

        Service.Log.Information($"[Vnav] {(fly ? "flyto" : "moveto")} request={position} snapped={target} player={playerPosition} distance={distance:F0}y issued={issued}");

        return issued;
    }

    /// <summary>
    /// Widen the vertical search until the mesh is hit, a marker placed from the map only carries a horizontal position
    /// and the height that was assumed for it can be far off.
    /// </summary>
    private static Vector3 SnapForNavigation(Vector3 position)
    {
        foreach (var verticalSearch in VerticalSearchSteps)
        {
            if (TrySnap(position, 5.0f, verticalSearch) is not { } snapped) continue;

            Service.Log.Information($"[Vnav] snapped with vertical search {verticalSearch:F0}y, height offset {snapped.Y - position.Y:F1}");

            return snapped;
        }

        Service.Log.Warning("[Vnav] no navmesh point found, using the raw position");

        return position;
    }

    private static Vector3? TrySnap(Vector3 position, float horizontalSearch, float verticalSearch)
    {
        try
        {
            EnsureInitialized();
            return nearestPoint!.InvokeFunc(position, horizontalSearch, verticalSearch);
        }
        catch
        {
            return null;
        }
    }
}
