using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Internal.Classes;
using Lumina.Text.ReadOnly;

namespace KamiToolKit.Nodes;

/// <summary>
/// Specialization of <see cref="DragDropNode"/> that has handy accessors for things used to represent a hotbar slot.
/// </summary>
public partial class HotbarNode : DragDropNode {

    /// <summary>
    /// Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public TextNode KeybindTextNode { get; }

    /// <summary>
    /// Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public TextNode ItemCountTextNode { get; }

    /// <summary>
    /// Updates the hotbar slots current state, cost, icon, and various other fields.
    /// </summary>
    public void Update() {
        UpdateSlotAppearance();

        TryProcessKeybind();

        if (lastClickTime is not null && DateTime.UtcNow - lastClickTime > TimeSpan.FromMilliseconds(330)) {
            IconNode.IconExtras.Timeline?.PlayAnimation(4);
            lastClickTime = null;
        }
    }

    /// <summary>
    /// Gets or sets the keybind that will activate this slot.
    /// </summary>
    public KeySetting? KeyBind { get; set; }

    /// <summary>
    /// Gets or sets whether this slot polls <see cref="KeyBind"/> on every update and consumes the
    /// pressed key. Owners that handle key presses themselves can disable this while still keeping
    /// <see cref="KeyBind"/> set, so the key label continues to be drawn on the slot.
    /// </summary>
    public bool HandleKeyBind { get; set; } = true;

    /// <summary>
    /// Maps a native slot type back to the drag and drop type that represents it.
    /// </summary>
    /// <remarks>
    /// Built by asking the game for the mapping in the other direction, so that every type the game knows
    /// about is covered without maintaining a hand written table.
    /// </remarks>
    private static readonly Dictionary<RaptureHotbarModule.HotbarSlotType, DragDropType> PayloadTypes = BuildPayloadTypes();

    private static Dictionary<RaptureHotbarModule.HotbarSlotType, DragDropType> BuildPayloadTypes() {
        var map = new Dictionary<RaptureHotbarModule.HotbarSlotType, DragDropType>();

        foreach (DragDropType type in Enum.GetValues<DragDropType>()) {
            if (type is DragDropType.Nothing or DragDropType.Everything) continue;

            RaptureHotbarModule.HotbarSlotType slotType = UIGlobals.GetHotbarSlotTypeFromDragDropType(type);

            if (slotType is RaptureHotbarModule.HotbarSlotType.Empty) continue;

            // The first match wins, which keeps the most specific drag type for a slot type.
            map.TryAdd(slotType, type);
        }

        return map;
    }

    /// <summary>
    /// Gets whether this slot is empty.
    /// </summary>
    /// <remarks>
    /// Slot types that have no drag and drop counterpart, such as key items, are written directly to the
    /// native slot, so the payload alone would report them as empty.
    /// </remarks>
    public unsafe bool IsEmpty
        => Payload.Type is 0 or DragDropType.Nothing
            && hotbarData->CommandType is RaptureHotbarModule.HotbarSlotType.Empty;

    /// <summary>
    /// Gets the apparent (displayed) action id of this slot.
    /// </summary>
    /// <remarks>
    /// This can differ from the stored command when the game substitutes the action, for example when a
    /// dancer step turns another weapon skill into the step that is currently available.
    /// </remarks>
    public unsafe uint ApparentActionId => hotbarData->ApparentActionId;

    /// <summary>
    /// Gets the apparent (displayed) slot type of this slot.
    /// </summary>
    public unsafe RaptureHotbarModule.HotbarSlotType ApparentSlotType => hotbarData->ApparentSlotType;

    /// <summary>
    /// Sets this hotbar slot from the provided payload information.
    /// </summary>
    public void SetSlot(DragDropPayload payload)
        => SetHotbarSlotFromPayload(payload);

    /// <summary>
    /// Sets this hotbar slot to the specific type and id.
    /// </summary>
    public void SetSlot(DragDropType type, uint id) {
        SetHotbarSlotFromPayload(new DragDropPayload {
            Type = type,
            Int2 = (int) id,
        });
    }

    /// <summary>
    /// Clears the data for this hotbar slot.
    /// </summary>
    public unsafe void ClearSlot() {
        Payload.Clear();

        hotbarState->Ctor();
        ResetHotbarSlot(hotbarData);

        // The icon is not part of the slot data, so it has to be cleared explicitly, otherwise the icon of
        // the removed content stays on screen.
        IconId = 0;
    }

    /// <summary>
    /// Resets the content of a hotbar slot.
    /// </summary>
    /// <remarks>
    /// The native clear function of the game expects the slot to be fully initialized, which is not the
    /// case for slots that are created by hand, so the fields of the slot are reset directly instead.
    /// </remarks>
    private static unsafe void ResetHotbarSlot(RaptureHotbarModule.HotbarSlot* slot) {
        slot->CommandType = RaptureHotbarModule.HotbarSlotType.Empty;
        slot->CommandId = 0;

        slot->ApparentSlotType = RaptureHotbarModule.HotbarSlotType.Empty;
        slot->ApparentActionId = 0;
        slot->ApparentActionModeParam = 0;

        slot->OriginalApparentSlotType = RaptureHotbarModule.HotbarSlotType.Empty;
        slot->OriginalApparentActionId = 0;

        slot->UsesGeneralDragDropType = false;
        slot->IsLoaded = false;

        slot->CostType = 0;
        slot->CostDisplayMode = 0;
        slot->CostValue = 0;

        slot->IconId = 0;

        slot->RecipeValid = 0;
        slot->RecipeDataLoaded = false;
        slot->RecipeItemId = 0;
        slot->RecipeCraftType = 0;
    }

    /// <summary>
    /// Sets this hotbar slot to the specific type and id.
    /// </summary>
    public void SetSlot(DragDropType payloadType, int payloadInt)
        => SetSlot(payloadType, (uint)payloadInt);

    /// <summary>
    /// Function that is called when the associated <see cref="KeyBind"/> is pressed.
    /// </summary>
    protected virtual void OnKeybindPressed()
        => OnHotbarNodeClicked(this);

    /// <summary>
    /// Plays the press animation of this slot.
    /// </summary>
    /// <remarks>
    /// Executing a slot only replays the icon timeline, so this is what gives the slot a visible
    /// confirmation. It is triggered from the click handler so that both a mouse click and an external
    /// key binding produce the same feedback.
    /// </remarks>
    public void PlayClickFeedback() {
        IconNode.IconExtras.Timeline?.PlayAnimation(3, true);
        lastClickTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets this hotbar slot to the specified action.
    /// </summary>
    public void SetAction(uint actionId) {
        SetHotbarSlotFromPayload(new DragDropPayload {
            Type = DragDropType.Action,
            Int2 = (int) actionId,
        });
    }

    /// <summary>
    /// Sets this hotbar slot to an inventory item, addressed by its item id.
    /// </summary>
    /// <remarks>
    /// The regular drag and drop path stores the position of the item inside the inventory, which does not
    /// survive a reload or a rearrangement of the inventory. This overload stores the item id instead, so
    /// that the slot can be persisted and restored later.
    /// </remarks>
    /// <param name="itemId">The item id to place in the slot.</param>
    public unsafe void SetSlotItem(uint itemId) {
        Payload = new DragDropPayload {
            Type = DragDropType.Item,
            Int2 = (int) itemId,
        };

        ResetHotbarSlot(hotbarData);
        hotbarData->Set(RaptureHotbarModule.HotbarSlotType.Item, itemId);

        UpdateSlotAppearance();
    }

    /// <summary>
    /// Sets this hotbar slot to the specified macro.
    /// </summary>
    /// <param name="macroIndex">The index of the macro in the macro list.</param>
    public unsafe void SetSlotMacro(uint macroIndex) {
        Payload = new DragDropPayload {
            Type = DragDropType.Macro,
            Int2 = (int) macroIndex,
        };

        ResetHotbarSlot(hotbarData);
        hotbarData->Set(RaptureHotbarModule.HotbarSlotType.Macro, macroIndex);

        UpdateSlotAppearance();
    }

    /// <summary>
    /// Sets this slot to the given native slot type and id.
    /// </summary>
    /// <remarks>
    /// Used for slot types that have no dedicated setter, such as key items, main commands or mounts. The
    /// payload is rebuilt from the native type so that the slot can be dragged out and dropped onto another
    /// slot again.
    /// </remarks>
    public unsafe void SetSlot(RaptureHotbarModule.HotbarSlotType type, uint id) {
        Payload = CreatePayload(type, id);

        ResetHotbarSlot(hotbarData);
        hotbarData->Set(type, id);

        UpdateSlotAppearance();
    }

    /// <summary>
    /// Rebuilds the native slot from the payload this slot currently holds.
    /// </summary>
    /// <remarks>
    /// Used after a swap: the source slot receives the payload of the drop target without going through the
    /// insert handler, so its native slot has to be written from the new payload explicitly.
    /// </remarks>
    public unsafe void SetSlotFromCurrentPayload() {
        if (Payload.Type is 0 or DragDropType.Nothing) {
            ClearSlot();

            return;
        }

        SetHotbarSlotFromPayload(Payload);

        Update();
    }

    /// <summary>
    /// Builds the drag and drop payload that represents the given native slot type and id.
    /// </summary>
    /// <remarks>
    /// Key items and crystals carry their id in the reference index and require a location marker, which is
    /// what the native setter of those types validates before accepting a payload.
    /// </remarks>
    private static DragDropPayload CreatePayload(RaptureHotbarModule.HotbarSlotType type, uint id) {
        if (!PayloadTypes.TryGetValue(type, out DragDropType payloadType)) return new DragDropPayload();

        return new DragDropPayload {
            Type           = payloadType,
            Int1           = type switch {
                RaptureHotbarModule.HotbarSlotType.InventoryItem => 48,
                RaptureHotbarModule.HotbarSlotType.KeyItem       => 7,
                RaptureHotbarModule.HotbarSlotType.Crystal       => 9,
                _                                                => 0,
            },
            Int2           = (int) id,
            ReferenceIndex = type is RaptureHotbarModule.HotbarSlotType.KeyItem
                                  or RaptureHotbarModule.HotbarSlotType.Crystal
                ? (short) id
                : (short) 0,
        };
    }

    /// <summary>
    /// Gets the content of this slot, or null when the slot is empty.
    /// </summary>
    public unsafe HotbarSlotContent? GetSlotContent() {
        var type = hotbarData->CommandType;

        // IsValid additionally requires the apparent slot type, which the native render only produces for
        // actions and ordinary items. Key items, crystals and event items never get one, so relying on
        // IsValid reported them as empty and their content was dropped when the bar was saved.
        if (type is RaptureHotbarModule.HotbarSlotType.Empty) return null;

        // Items that were dragged in are stored by their position inside the inventory, which is not stable
        // across sessions. The apparent action id holds the item id for every item slot type, so it is used
        // here to make the content persistable. It is only filled once the slot has been rendered, so the
        // command id is used as a fallback.
        uint apparent = hotbarData->ApparentActionId;
        uint command  = hotbarData->CommandId;

        uint id = type is RaptureHotbarModule.HotbarSlotType.Item
                          or RaptureHotbarModule.HotbarSlotType.InventoryItem
                          or RaptureHotbarModule.HotbarSlotType.EventItem
            ? apparent is not (0 or uint.MaxValue) ? apparent : command
            : command;

        // Both zero and uint.MaxValue are used by the game to mean "no id". Reporting one of them would
        // overwrite the stored data with something that cannot be restored.
        if (id is 0 or uint.MaxValue) return null;

        return new HotbarSlotContent(type, id);
    }

    /// <inheritdoc />
    public HotbarNode() {
        KeybindTextNode = new TextNode {
            NodeId = 4,
            Position = new Vector2(1.0f, -6.0f),
            Size = new Vector2(50.0f, 20.0f),
            FontType = FontType.MiedingerMed,
            TextColor = KnownColor.White.Vector(),
            TextOutlineColor = new Vector4(0.200f, 0.200f, 0.200f, 1.000f),
            TextFlags = TextFlags.Edge | TextFlags.Ellipsis | (TextFlags) 0x8000,
        };
        KeybindTextNode.AttachNode(this);

        ItemCountTextNode = new TextNode {
            NodeId = 5,
            Position = new Vector2(4.0f, 34.0f),
            Size = new Vector2(40.0f, 12.0f),
            TextColor = KnownColor.White.Vector(),
            TextOutlineColor = ColorHelper.GetColor(55),
            AlignmentType = AlignmentType.Right,
            TextFlags = TextFlags.Edge | (TextFlags) 0x8000,
        };
        ItemCountTextNode.AttachNode(this);

        OnRollOver = OnHotbarNodeRollOver;
        OnRollOut = OnHotbarNodeRollOut;
        OnPayloadAccepted = OnHotbarNodePayloadAccepted;
        OnClicked = OnHotbarNodeClicked;
        OnDiscard = OnHotbarNodeDiscard;
        OnBegin = OnDragDropBegin;
        OnEnd = OnDragDropEnd;

        unsafe {
            hotbarData = IMemorySpace.GetUISpace()->MallocZeroed<RaptureHotbarModule.HotbarSlot>();
            hotbarData->Initialize();

            // The slot carries a native Utf8String that the game expects to be constructed on its own.
            // The native clear and prepare functions of the slot work on that string pointer, so without
            // this the game reads uninitialized memory and crashes as soon as a payload is dropped onto
            // the slot.
            hotbarData->PopUpHelp.Ctor();

            hotbarState = IMemorySpace.GetUISpace()->MallocZeroed<RaptureHotbarModule.HotbarUIIntermediate>();
            hotbarState->Ctor();
        }

        IGameGui.Get().AgentUpdate += OnAgentUpdate;
        RegisterDebugWindow();
    }

    /// <inheritdoc />
    protected override unsafe void Dispose(bool isNativeDestructor) {
        if (IsDisposed) return;

        base.Dispose(isNativeDestructor);

        IGameGui.Get().AgentUpdate -= OnAgentUpdate;
        UnregisterDebugWindow();

        // The drag source reference is static and shared by every slot, so it must not point at a node
        // that is about to be freed.
        if (ReferenceEquals(dragSourceNode, this)) dragSourceNode = null;

        hotbarData->PopUpHelp.Dtor();
        IMemorySpace.Free(hotbarData, 0);
        IMemorySpace.Free(hotbarState, 0);
    }

    private void OnAgentUpdate(AgentUpdateFlag agentFlags) {
        if (!agentFlags.HasFlag(AgentUpdateFlag.InventoryUpdate)
            && !agentFlags.HasFlag(AgentUpdateFlag.ActionBarUpdate)) return;

        // A slot without a payload was never set through a drag and drop, so there is nothing to rebuild.
        if (Payload.Type is 0 or DragDropType.Nothing) return;

        SetHotbarSlotFromPayload(Payload);
    }

    private unsafe void OnHotbarNodeRollOver(DragDropNode thisNode) {
        HideTooltip();

        if (!hotbarData->IsValid) {
            // A slot whose content was removed must stop advertising the tooltip of its previous content.
            // Clearing the text also unregisters the native roll-over handler.
            TextTooltip   = default;
            ActionTooltip = 0;
            ItemTooltip   = 0;

            return;
        }

        TextTooltip = hotbarData->PopUpHelp.AsReadOnlySeString();

        switch (hotbarData->CommandType) {
            case RaptureHotbarModule.HotbarSlotType.Action:
                ActionTooltip = hotbarData->CommandId;
                ItemTooltip = 0;
                break;

            case RaptureHotbarModule.HotbarSlotType.Macro when hotbarData->ApparentSlotType is RaptureHotbarModule.HotbarSlotType.Action:
                ActionTooltip = hotbarData->ApparentActionId;
                ItemTooltip = 0;
                break;

            case RaptureHotbarModule.HotbarSlotType.Item:
                ActionTooltip = 0;
                ItemTooltip = hotbarData->ApparentActionId;
                break;

            default:
                ActionTooltip = 0;
                ItemTooltip = 0;
                break;
        }

        ShowTooltip();
    }

    private void OnHotbarNodeRollOut(DragDropNode thisNode) {
        HideTooltip();
    }

    private unsafe void OnHotbarNodePayloadAccepted(DragDropNode thisNode, DragDropPayload payload) {
        if (settingSourceNodeData) return;

        // Keep a local reference: a discard below clears the static drag source, and the source still has
        // to be refreshed afterwards.
        HotbarNode? source = dragSourceNode;

        // If source is another KTK Node
        if (source is not null) {

            // And we are not empty, we need to swap our slots.
            if (hotbarData->IsValid) {
                var sourcePayload = source.Payload.Clone();

                // Call OnPayloadAccepted to trigger any down-stream events that are listening for payload change.
                try {
                    settingSourceNodeData = true;
                    source.OnPayloadAccepted?.Invoke(source, Payload);
                }

                // Ensure this gets cleared, or this'll prevent all HotbarNodes from accepting payloads.
                finally {
                    settingSourceNodeData = false;
                }

                source.Payload = Payload.Clone();
                Payload = sourcePayload;
            }

            // If we are empty, take the sources payload, and tell it to discard what it has
            else {
                Payload = source.Payload.Clone();
                source.OnDiscard?.Invoke(source);
            }

            // The source has to rebuild its native slot from the payload it was given. This is what makes
            // a swap actually move content between the two slots.
            source.SetSlotFromCurrentPayload();
        }

        // Source is a vanilla node
        else {

            // If source is a native slot, eventually write this code to get the HotbarSlot* and clear it
            // But too lazy for that right now.

            Payload = payload.Clone();
        }

        SetHotbarSlotFromPayload(Payload);
        Update();
    }

    private unsafe void OnHotbarNodeClicked(DragDropNode thisNode) {
        // Clicking an empty slot must be a no-op: executing an invalid slot still touches the game's
        // hotbar module and leaves it half-way into a drag session.
        if (!hotbarData->IsValid) return;

        var hotbarModule = RaptureHotbarModule.Instance();
        if (hotbarModule is null) return;

        hotbarModule->ExecuteSlot(hotbarData);

        PlayClickFeedback();
    }

    private void OnHotbarNodeDiscard(DragDropNode thisNode) {
        // A discard (dropping onto the world, or cancelling the drag with escape) also ends the drag.
        // Without this the node stays referenced as the drag source, and the next drop coming from the
        // vanilla hotbar or the inventory would be treated as a slot-to-slot swap instead of a replace.
        if (ReferenceEquals(dragSourceNode, thisNode)) dragSourceNode = null;

        ClearSlot();
    }

    private void OnDragDropBegin(DragDropNode node) {
        if (node is not HotbarNode hotbarNode) return;

        dragSourceNode = hotbarNode;
    }

    private void OnDragDropEnd(DragDropNode node) {
        dragSourceNode = null;
    }

    private unsafe void TryProcessKeybind() {
        if (!HandleKeyBind) return;
        if (ICondition.Get().Any(ConditionFlag.OccupiedInQuestEvent)) return;
        if (KeyBind is not { Key: not SeVirtualKey.NO_KEY } keyBind) return;
        if (RaptureAtkModule.Instance()->IsTextInputActive()) return;

        var keyStateService = IKeyState.Get();
        if (!keyStateService.IsVirtualKeyValid((int)keyBind.Key)) return;

        // Main key isn't pressed
        if (!keyStateService[(int)keyBind.Key]) return;

        // Only allow one modifier key, with priority Ctrl -> Alt -> Shift
        VirtualKey? modifierKey = keyBind.KeyModifier switch {
            _ when keyBind.KeyModifier.HasFlag(KeyModifierFlag.Ctrl) => VirtualKey.CONTROL,
            _ when keyBind.KeyModifier.HasFlag(KeyModifierFlag.Alt) => VirtualKey.MENU,
            _ when keyBind.KeyModifier.HasFlag(KeyModifierFlag.Shift) => VirtualKey.SHIFT,
            _ => null,
        };

        // If modifier is required
        if (modifierKey is { } modifier) {

            // But isn't valid, return.
            if (!keyStateService.IsVirtualKeyValid(modifier)) {
                return;
            }

            // Or isn't pressed, return.
            if (!keyStateService[modifier]) {
                return;
            }
        }

        // Keybind doesn't use a modifier, check if any modifier is present and ignore if it is pressed.
        else if (modifierKey is null) {

            // If control is valid and is pressed, return
            if (keyStateService.IsVirtualKeyValid(VirtualKey.CONTROL)) {
                if (keyStateService[VirtualKey.CONTROL]) {
                    return;
                }
            }

            // If alt is valid and is pressed, return
            if (keyStateService.IsVirtualKeyValid(VirtualKey.MENU)) {
                if (keyStateService[VirtualKey.MENU]) {
                    return;
                }
            }

            // If shift is valid and is pressed, return
            if (keyStateService.IsVirtualKeyValid(VirtualKey.SHIFT)) {
                if (keyStateService[VirtualKey.SHIFT]) {
                    return;
                }
            }
        }

        // Modifier (if any), and main key is pressed here.

        // Clear the pressed key, leave modifiers pressed.
        keyStateService[(int)keyBind.Key] = false;

        OnKeybindPressed();
    }

    /// <summary>
    /// Gets the displayed string used to represent a keybind.
    /// </summary>
    public static ReadOnlySeString GetKeybindText(KeySetting? keybind) {
        if (keybind is not {} keyBind) return string.Empty;

        ReadOnlySeString? modifierKey = keyBind.KeyModifier switch {
            _ when keyBind.KeyModifier.HasFlag(KeyModifierFlag.Ctrl) => "¢",
            _ when keyBind.KeyModifier.HasFlag(KeyModifierFlag.Alt) => "ª",
            _ when keyBind.KeyModifier.HasFlag(KeyModifierFlag.Shift) => "§",
            _ => null,
        };

        return $"{modifierKey}{(char)keyBind.Key}";
    }

    private unsafe void UpdateSlotAppearance() {
        var hotbarModule = RaptureHotbarModule.Instance();
        if (hotbarModule is null) return;

        var isMacro = hotbarData->CommandType is RaptureHotbarModule.HotbarSlotType.Macro;
        var isItem = hotbarData->CommandType is RaptureHotbarModule.HotbarSlotType.Item;

        // Clear hotbar state to get fresh data.
        hotbarState->Ctor();

        RaptureHotbarModule.HotbarSlotType outType;
        uint outActionId;
        ushort unkC4;

        RaptureHotbarModule.GetSlotAppearance(&outType, &outActionId, &unkC4, hotbarModule, hotbarData);
        hotbarData->ApparentActionId = outActionId;
        hotbarData->ApparentSlotType = outType;

        RaptureHotbarModule.Instance()->PrepareSlotForRender(hotbarData, hotbarState);

        // An empty slot has no icon. The native render reports uint.MaxValue for it, and the icon component
        // draws that as a solid black box instead of nothing, so the id has to be cleared explicitly.
        uint iconId = hotbarData->IsValid ? hotbarState->IconId : 0;

        if (IconId != iconId) IconId = iconId;

        // IsBackgroundShow tells us we wanna force this slot to be visible.
        IsVisible = hotbarData->IsValid || IsBackgroundShown;

        var isAvailable = hotbarState->ActionAvailable1 && hotbarState->ActionAvailable2;

        IconNode.IsFaded = !isAvailable && !isMacro;
        IconNode.ShowMacroIcon = isMacro;

        IconNode.ResourceCostVisible = hotbarState->CostType is 2 or 5 or 4; // Mana or GP or CP
        IconNode.ResourceCostValue = hotbarState->CostValue;

        IconNode.CostTextColor = hotbarState->CostType switch {
            2 => CostTextColor.Mana,
            4 => CostTextColor.DoH,
            5 => CostTextColor.DoL,
            _ => CostTextColor.Mana,
        };
        IconNode.IsInvalid = !hotbarState->ActionTargetSatisfied;

        IconNode.ChargeCountVisible = hotbarState->CooldownMode is 3;
        IconNode.ChargeCount = hotbarState->CurrentChargeCount;
        IconNode.ChargePercent = hotbarState->ChargePercent / 100.0f;

        IconNode.CooldownSecondsVisible = hotbarState->CooldownSeconds is not 0;
        IconNode.CooldownSeconds = hotbarState->CooldownSeconds;

        IconNode.CooldownPercentVisible = hotbarState->CooldownPercent is not 0;
        IconNode.CooldownPercent = hotbarState->CooldownPercent / 100.0f;

        IconNode.IsAnts = hotbarState->DrawAnts;

        ItemCountTextNode.IsVisible = !IconNode.CooldownSecondsVisible && isItem;
        if (hotbarData->CostDisplayMode is 2 or 4) {
            ItemCountTextNode.String = hotbarData->CostTextString;
        }
        else {
            ItemCountTextNode.String = string.Empty;
        }

        KeybindTextNode.String = KeyBind is null ? string.Empty : GetKeybindText(KeyBind);
        KeybindTextNode.IsVisible = KeyBind is not null && hotbarData->IsValid || IsBackgroundShown;
    }

    private unsafe void SetHotbarSlotFromPayload(DragDropPayload payload) {
        var hotbarSlotType = UIGlobals.GetHotbarSlotTypeFromDragDropType(payload.Type);

        IPluginLog.Get().Verbose("HotbarSlot received payload:\n" +
                                 $"Type: {payload.Type}\n" +
                                 $"Int1: {payload.Int1}\n" +
                                 $"Int2: {payload.Int2}\n" +
                                 $"ReferenceIndex: {payload.ReferenceIndex}");


        switch (hotbarSlotType) {
            case RaptureHotbarModule.HotbarSlotType.InventoryItem:

                if (payload.Int1 is not (48 or 49 or 50 or 51)) {
                    IPluginLog.Get().Verbose("Received item from invalid location, skipping setting hotbar.");
                    return;
                }

                var sorterEntry = ItemOrderModule.Instance()->InventorySorter->Items[payload.ReferenceIndex].Value;
                var id = (uint)sorterEntry->Page << 16 | (uint)sorterEntry->Slot & 0xFFFF;

                Payload = payload.Clone();
                ResetHotbarSlot(hotbarData);
                hotbarData->Set(hotbarSlotType, id);
                return;

            case RaptureHotbarModule.HotbarSlotType.KeyItem:
                if (payload.Int1 is not 7) {
                    IPluginLog.Get().Verbose("Received key item from invalid location, skipping setting hotbar.");
                    return;
                }

                Payload = payload.Clone();
                ResetHotbarSlot(hotbarData);
                hotbarData->Set(hotbarSlotType, (uint) payload.ReferenceIndex);
                return;

            case RaptureHotbarModule.HotbarSlotType.Crystal:
                if (payload.Int1 is not 9) {
                    IPluginLog.Get().Verbose("Received crystal item from invalid location, skipping setting hotbar.");
                    return;
                }

                Payload = payload.Clone();
                ResetHotbarSlot(hotbarData);
                hotbarData->Set(hotbarSlotType, (uint) payload.ReferenceIndex);
                return;

            default:
                Payload = payload.Clone();
                ResetHotbarSlot(hotbarData);
                hotbarData->Set(hotbarSlotType, (uint) Payload.Int2);
                return;
        }
    }

    private readonly unsafe RaptureHotbarModule.HotbarSlot* hotbarData;
    private readonly unsafe RaptureHotbarModule.HotbarUIIntermediate* hotbarState;
    private DateTime? lastClickTime;

    private static HotbarNode? dragSourceNode;
    private static bool settingSourceNodeData;
}
