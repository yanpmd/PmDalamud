using System.Globalization;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace Chat2Ex.GameFunctions;

public unsafe class GameFunctions : IDisposable
{
    #region Hooks
    [Signature("E8 ?? ?? ?? ?? 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B D0 49 8D 4F", DetourName = nameof(ResolveTextCommandPlaceholderDetour))]
    private Hook<ResolveTextCommandPlaceholderDelegate>? ResolveTextCommandPlaceholderHook = null!;
    private delegate nint ResolveTextCommandPlaceholderDelegate(nint a1, byte* placeholderText, byte a3, byte a4);
    #endregion

    private Plugin Plugin { get; }
    public KeybindManager KeybindManager { get; }
    public Chat Chat { get; }

    public GameFunctions(Plugin plugin)
    {
        Plugin = plugin;
        KeybindManager = new KeybindManager(plugin);
        Chat = new Chat(Plugin);

        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        ResolveTextCommandPlaceholderHook?.Enable();
    }

    public void Dispose()
    {
        Chat.Dispose();
        KeybindManager.Dispose();

        ResolveTextCommandPlaceholderHook?.Dispose();

        Marshal.FreeHGlobal(PlaceholderNamePtr);
    }

    public void SendFriendRequest(string name, ushort world)
    {
        ListCommand(name, world, "friendlist");
    }

    public void AddToBlacklist(string name, ushort world)
    {
        ListCommand(name, world, "blist");
    }

    public void AddToMuteList(ulong accountId, ulong contentId, string name, short worldId)
    {
        AgentMutelist.Instance()->Add(accountId, contentId, name, worldId);
    }

    public void AddToTermsList(SeString content)
    {
        AgentTermFilter.Instance()->OpenNewFilterWindow(content.EncodeWithNullTerminator());
    }

    private void ListCommand(string name, ushort world, string commandName)
    {
        var worldRow = Sheets.WorldSheet.GetRow(world);

        ReplacementName = $"{name}@{worldRow.Name.ToString()}";
        ChatBox.SendMessage($"/{commandName} add {Placeholder}");
    }

    private static T* GetAddon<T>(string name) where T : unmanaged
    {
        // The game tears down the UI on login/zone changes; RaptureAtkModule is null in that window.
        // This is called from Framework.Update every tick, so a null dereference here kills the process.
        var raptureAtkModule = RaptureAtkModule.Instance();
        if (raptureAtkModule == null)
            return null;

        var addon = raptureAtkModule->RaptureAtkUnitManager.GetAddonByName(name);
        return addon != null && addon->IsReady ? (T*)addon : null;
    }

    public static void SetAddonInteractable(string name, bool interactable)
    {
        var addon = GetAddon<AtkUnitBase>(name);
        if (addon == null)
            return;
        addon->IsVisible = interactable;
    }

    public static void SetChatInteractable(bool interactable)
    {
        for (var i = 0; i < 4; i++)
            SetAddonInteractable($"ChatLogPanel_{i}", interactable);

        SetAddonInteractable("ChatLog", interactable);
    }

    public static bool IsAddonInteractable(string name)
    {
        var addon = GetAddon<AtkUnitBase>(name);
        return addon != null && addon->IsVisible;
    }

    public static void OpenItemTooltip(uint id, ItemKind itemKind)
    {
        var atkStage = AtkStage.Instance();
        var agent = AgentItemDetail.Instance();
        var addon = GetAddon<AtkUnitBase>("ItemDetail");

        if (atkStage == null || agent == null || addon == null)
            return;

        if (!Plugin.ClientState.IsLoggedIn)
            return;

        agent->DetailKind = itemKind == ItemKind.EventItem ? DetailKind.KeyItem : DetailKind.Item;
        agent->TypeOrId = id;
        agent->Index = 0;
        agent->Flag1 &= 0xEF;
        agent->ItemId = id;

        // This just probably needs to be set
        agent->AddonId = addon->Id;

        // Skips early return
        atkStage->TooltipManager.TooltipType |= 2;
        addon->Show(false, 15);

        // The agent only rebuilds its content when this "dirty" flag is raised, without it the window
        // keeps showing the item that was opened last (e.g. hovering a potion showed a helmet).
        var refreshOffset = ItemDetailRefreshOffset;

        if (refreshOffset >= 0)
            *(byte*)((nint)agent + refreshOffset) = 1;
    }

    private static nint itemDetailRefreshOffset = nint.MinValue;

    /// <summary>
    ///     Offset of the "needs refresh" flag inside <c>AgentItemDetail</c>, located by signature so it
    ///     survives game updates instead of being hardcoded.
    /// </summary>
    private static unsafe nint ItemDetailRefreshOffset
    {
        get
        {
            if (itemDetailRefreshOffset != nint.MinValue)
                return itemDetailRefreshOffset;

            try
            {
                // 88 83 ?? ?? ?? ??   mov byte ptr [rbx+disp32], al
                var address = Plugin.SigScanner.ScanText("88 83 ?? ?? ?? ?? 48 8B 5C 24 ?? 48 8B 6C 24");
                itemDetailRefreshOffset = Marshal.ReadInt32(address + 2);
                Plugin.Log.Debug($"ItemDetail refresh flag offset: 0x{itemDetailRefreshOffset:X}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Failed to locate the ItemDetail refresh flag, item tooltips may show stale data");
                itemDetailRefreshOffset = -1;
            }

            return itemDetailRefreshOffset;
        }
    }

    public static void CloseItemTooltip()
    {
        // hide addon first to prevent the "addon close" sound
        var addon = GetAddon<AtkUnitBase>("ItemDetail");
        if (addon != null)
            addon->Hide(true, false, 0);

        var agent = AgentItemDetail.Instance();
        if (agent != null)
        {
            var eventData = stackalloc AtkValue[1];
            var atkValues = stackalloc AtkValue[1];
            atkValues->Type = ValueType.Int;
            atkValues->Int = -1;
            agent->ReceiveEvent(eventData, atkValues, 1, 1);
        }
    }

    public static void OpenPartyFinder()
    {
        // this whole method: 6.05: 84433A (FF 97 ?? ?? ?? ?? 41 B4 01)
        var lfg = AgentLookingForGroup.Instance();
        if (lfg is null)
            return;

        if (lfg->IsAgentActive())
        {
            var addonId = lfg->GetAddonId();
            var atkModule = RaptureAtkModule.Instance();
            if (atkModule is null)
                return;

            var atkModuleVtbl = (void**) atkModule->AtkModule.VirtualTable;
            var vf27 = (delegate* unmanaged<RaptureAtkModule*, ulong, ulong, byte>) atkModuleVtbl[27];
            vf27(atkModule, addonId, 1);
        }
        else
        {
            // 6.05: 8443DD
            if (*(uint*) ((nint) lfg + 0x2C20) > 0)
                lfg->Hide();
            else
                lfg->Show();
        }
    }

    public static bool IsMentor()
    {
        var state = PlayerState.Instance();
        return state is not null && state->IsMentor();
    }

    public static InfoProxyCommonList.CharacterData[] GetFriends()
    {
        var friendList = InfoProxyFriendList.Instance();
        if (friendList is null)
            return [];

        return friendList->CharDataSpan.ToArray();
    }

    public static void OpenQuestLog(RowRef<Quest> quest)
    {
        if (!quest.IsValid)
        {
            Plugin.ChatGui.PrintError("Quest is wrongly formatted");
            return;
        }

        var splits = quest.Value.Id.ToString().Split("_");
        if (splits.Length != 2)
        {
            Plugin.ChatGui.PrintError("QuestId is wrongly formatted");
            return;
        }

        if (!uint.TryParse(splits[1], NumberStyles.Any, CultureInfo.InvariantCulture,  out var questId))
        {
            Plugin.ChatGui.PrintError("Unable to parse quest id");
            return;
        }

        AgentQuestJournal.Instance()->OpenForQuest(questId, 1);
    }

    public static void OpenPartyFinder(uint id)
    {
        var lfg = AgentLookingForGroup.Instance();
        if (lfg is null)
            return;

        lfg->OpenListing(id);
    }

    public static void OpenAchievement(uint id)
    {
        var achievement = AgentAchievement.Instance();
        if (achievement is null)
            return;

        achievement->OpenById(id);
    }

    public static bool IsInInstance()
    {
        return Plugin.Condition[ConditionFlag.BoundByDuty56];
    }

    public static bool TryOpenAdventurerPlate(ulong playerId)
    {
        try
        {
            AgentCharaCard.Instance()->OpenCharaCard(playerId);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "Unable to open adventurer plate");
            return false;
        }
    }

    public static void ClickNoviceNetworkButton()
    {
        var agent = AgentChatLog.Instance();
        // case 3
        var value = new AtkValue { Type = ValueType.Int, Int = 3, };
        var result = 0;
        var vf0 = *(delegate* unmanaged<AgentChatLog*, int*, AtkValue*, ulong, ulong, int*>*) agent->VirtualTable;
        vf0(agent, &result, &value, 0, 0);
    }

    private readonly nint PlaceholderNamePtr = Marshal.AllocHGlobal(128);
    private readonly string Placeholder = $"<{Guid.NewGuid():N}>";
    private string? ReplacementName;

    private nint ResolveTextCommandPlaceholderDetour(nint a1, byte* placeholderText, byte a3, byte a4)
    {
        try
        {
            var placeholder = MemoryHelper.ReadStringNullTerminated((nint) placeholderText);
            if (ReplacementName == null || placeholder != Placeholder)
                return ResolveTextCommandPlaceholderHook!.Original(a1, placeholderText, a3, a4);

            MemoryHelper.WriteString(PlaceholderNamePtr, ReplacementName);
            ReplacementName = null;

            return PlaceholderNamePtr;
        }
        catch (Exception ex)
        {
            // Game-thread detour: never let a managed exception cross the ReversePInvoke boundary.
            Chat2Ex.Native.NativeCallback.LogError(ex, "error in ResolveTextCommandPlaceholder detour");
            return ResolveTextCommandPlaceholderHook!.Original(a1, placeholderText, a3, a4);
        }
    }
}
