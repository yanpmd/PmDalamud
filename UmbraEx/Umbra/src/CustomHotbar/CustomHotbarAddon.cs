using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using ActionSheet = Lumina.Excel.Sheets.Action;
using HotbarSlotType = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.HotbarSlotType;

namespace Umbra.CustomHotbar;

/// <summary>
/// A native game window that shows a grid of hotbar slots.
/// </summary>
/// <remarks>
/// <para>
/// The slots are built on top of <see cref="HotbarNode"/>, which provides the native drag and drop
/// behaviour of the game as well as the native appearance of hotbar slots (cooldowns, costs, charges,
/// macro icons, ...). Clicking a slot executes it through the game's own hotbar module, which means
/// actions are cast, items are used and macros are run.
/// </para>
/// <para>
/// The window itself is stripped down to a bare frame: it has no title, no subtitle and no close button.
/// The frame can be hidden entirely, which leaves nothing but the slots.
/// </para>
/// </remarks>
internal sealed unsafe class CustomHotbarAddon : NativeAddon {
    /// <summary>
    /// The maximum number of slots this bar can hold.
    /// </summary>
    public const int MaxSlots = 120;

    /// <summary>
    /// The default number of slots that are shown in one row.
    /// </summary>
    public const int DefaultSlotsPerRow = 10;

    /// <summary>
    /// The maximum number of slots per row.
    /// </summary>
    /// <remarks>
    /// A row is 44 pixels wide per slot, so values above this would put the bar outside of the screen
    /// long before the slot limit is reached.
    /// </remarks>
    public const int MaxSlotsPerRow = 30;

    private const float SlotWidth   = 44.0f;
    private const float SlotHeight  = 48.0f;
    private const float SlotSpacing = 4.0f;

    // The window node of the game keeps a small margin between its frame and the content area.
    private const float FrameMarginX = 4.0f;
    private const float FrameMarginY = 8.0f;

    private readonly List<HotbarNode>         _slots = [];
    private readonly List<HorizontalListNode> _rows  = [];

    private VerticalListNode?      _rootNode;
    private List<CustomHotbarSlot> _pendingSlots = [];
    private int                    _slotCount      = 8;
    private int                    _slotsPerRow    = DefaultSlotsPerRow;
    private bool                   _locked;
    private bool                   _hideEmptySlots;
    private bool                   _showBackground = true;
    private bool                   _contentVisible = true;
    private bool                   _nodesValid;
    private readonly HashSet<int>  _dirtySlots = [];

    /// <summary>
    /// The id of the bar this window belongs to. It is reported along with slot changes so that a
    /// manager owning several bars can tell which one was edited.
    /// </summary>
    public string BarId { get; set; } = string.Empty;

    /// <summary>
    /// Raised when the content of one of the slots has been changed by the user.
    /// </summary>
    public event Action<string, int[]>? OnSlotsChanged;

    /// <summary>
    /// The number of slots that are currently present in this window.
    /// </summary>
    public int SlotCount => _slots.Count;

    /// <summary>
    /// Calculates the size of the slot area of the bar for the given amount of slots.
    /// </summary>
    public static Vector2 GetContentSize(int slotCount, int slotsPerRow = DefaultSlotsPerRow) {
        slotCount   = Math.Clamp(slotCount, 1, MaxSlots);
        slotsPerRow = Math.Clamp(slotsPerRow, 1, MaxSlotsPerRow);

        int columns = Math.Min(slotCount, slotsPerRow);
        int rows    = (int) Math.Ceiling(slotCount / (double) columns);

        float contentWidth  = columns * SlotWidth + (columns - 1) * SlotSpacing;
        float contentHeight = rows * SlotHeight + (rows - 1) * SlotSpacing;

        return new Vector2(contentWidth, contentHeight);
    }

    /// <summary>
    /// Calculates the size of the whole window for the given amount of slots.
    /// </summary>
    public static Vector2 GetWindowSize(int slotCount, int slotsPerRow = DefaultSlotsPerRow) {
        Vector2 content = GetContentSize(slotCount, slotsPerRow);

        return new Vector2(content.X + FrameMarginX * 2, content.Y + FrameMarginY * 2);
    }

    /// <summary>
    /// Changes the number of slots in this window.
    /// </summary>
    public void SetSlotCount(int count) {
        count = Math.Clamp(count, 1, MaxSlots);

        if (count == _slotCount) return;

        _slotCount = count;

        if (_rootNode is null) return;

        ResizeWindow();
        Rebuild();
        ApplySlots();
    }

    /// <summary>
    /// Changes how many slots are shown per row.
    /// </summary>
    public void SetSlotsPerRow(int slotsPerRow) {
        slotsPerRow = Math.Clamp(slotsPerRow, 1, MaxSlotsPerRow);

        if (slotsPerRow == _slotsPerRow) return;

        _slotsPerRow = slotsPerRow;

        if (_rootNode is null) return;

        ResizeWindow();
        Rebuild();
        ApplySlots();
    }

    /// <summary>
    /// Locks or unlocks the slot layout of this bar.
    /// </summary>
    /// <remarks>
    /// A locked bar still accepts new contents in empty slots, but existing contents can no longer be
    /// moved to another slot or dragged out of the bar.
    /// </remarks>
    public void SetLocked(bool locked) {
        if (locked == _locked) return;

        _locked = locked;

        ApplySlotInteraction();
    }

    /// <summary>
    /// The position the game currently reports for this window, in UI coordinates.
    /// </summary>
    /// <remarks>
    /// Used to detect that the game moved the window on its own (on open, on a resolution change or when
    /// the layout is rebuilt), so that the configured position can be applied again.
    /// </remarks>
    public unsafe Vector2 WindowPosition {
        get {
            if (InternalAddon is null) return Vector2.Zero;

            short x = 0;
            short y = 0;

            InternalAddon->GetPosition(&x, &y);

            return new Vector2(x, y);
        }
    }

    /// <summary>
    /// Sets whether the frame of the bar is drawn.
    /// </summary>
    public void SetShowBackground(bool show) {
        if (_showBackground == show) return;

        _showBackground = show;

        ApplyBackgroundVisibility();
    }

    /// <summary>
    /// Shows or hides the contents of the bar without closing it.
    /// </summary>
    /// <remarks>
    /// The window stays open so that the slots keep their state and their hotkeys keep working while the
    /// bar is hidden.
    /// </remarks>
    public void SetContentVisible(bool visible) {
        if (_contentVisible == visible) return;

        _contentVisible = visible;

        ApplyContentVisibility();
    }

    /// <summary>
    /// Applies the current visibility to both the addon root and the node that holds the slots.
    /// </summary>
    /// <remarks>
    /// Both nodes have to be kept in sync. Reopening an addon rebuilds the whole node tree, and the
    /// hidden state is restored on the slot node in <see cref="OnSetup"/>. Restoring only the addon
    /// root would leave the slots invisible forever, which is what made an auto-hidden bar impossible
    /// to bring back with the mouse after a reload.
    /// </remarks>
    private void ApplyContentVisibility() {
        // A closed addon has no valid nodes anymore. The desired state stays in _contentVisible and is
        // applied again as soon as OnSetup has rebuilt the node tree.
        if (!_nodesValid || InternalAddon is null) return;

        if (_contentVisible) {
            RootNode.AddNodeFlags(NodeFlags.Visible);
            _rootNode?.AddNodeFlags(NodeFlags.Visible);
        }
        else {
            RootNode.RemoveNodeFlags(NodeFlags.Visible);
            _rootNode?.RemoveNodeFlags(NodeFlags.Visible);
        }
    }

    /// <summary>
    /// Writes the given slots into the window.
    /// </summary>
    public void LoadSlots(IReadOnlyList<CustomHotbarSlot> slots) {
        _pendingSlots = slots.Select(slot => slot.Clone()).ToList();

        if (_rootNode is null) return;

        ApplySlots();
    }

    /// <summary>
    /// Updates the stored contents of the slots without touching the live nodes.
    /// </summary>
    /// <remarks>
    /// This is used after the user has changed a slot by dragging something into it: the live nodes
    /// already contain the new content, so they must not be written to again.
    /// </remarks>
    public void SyncSlots(IReadOnlyList<CustomHotbarSlot> slots) {
        _pendingSlots = slots.Select(slot => slot.Clone()).ToList();
    }

    /// <summary>
    /// Changes the key that triggers the given slot.
    /// </summary>
    public void SetSlotHotkey(int index, VirtualKey key, KeyModifierFlag modifier) {
        if (index < 0 || index >= _slots.Count) return;

        while (_pendingSlots.Count <= index) {
            _pendingSlots.Add(new CustomHotbarSlot());
        }

        _pendingSlots[index].Hotkey         = key;
        _pendingSlots[index].HotkeyModifier = modifier;

        _slots[index].KeyBind = CreateKeyBind(key, modifier);
    }

    /// <summary>
    /// Reads the current content of one slot of this window.
    /// </summary>
    /// <returns>
    /// The captured slot, or null when the slot claims to have content but cannot report it. The caller
    /// must keep the stored value in that case instead of overwriting it.
    /// </returns>
    /// <remarks>
    /// Slots are captured one at a time so that a single unreadable slot can never wipe the whole bar.
    /// </remarks>
    public CustomHotbarSlot? CaptureSlot(int index) {
        if (index < 0 || index >= _slots.Count) return null;

        HotbarNode       node = _slots[index];
        CustomHotbarSlot slot = index < _pendingSlots.Count ? _pendingSlots[index].Clone() : new CustomHotbarSlot();

        if (node.IsEmpty) {
            slot.ClearContent();

            return slot;
        }

        HotbarSlotContent? content = node.GetSlotContent();

        if (content is null) return null;

        ReadSlot(content, slot);

        return slot;
    }

    /// <summary>
    /// Refreshes the appearance of the slots and reports a pending content change at most once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Slot contents are only read after a real drag and drop event. Polling the native slot every frame
    /// is unreliable: the game rewrites apparent action ids while actions are being used, so a mere key
    /// press used to look like a content change and rebuilt the settings window.
    /// </para>
    /// <para>
    /// This is called from the plugin's tick instead of the draw callback of the window, so that the
    /// slots keep their appearance up to date while the bar is hidden.
    /// </para>
    /// </remarks>
    public void UpdateSlots() {
        foreach (HotbarNode slot in _slots) {
            // An empty slot has nothing to render: its icon is cleared by ClearSlot, so the native slot
            // render is skipped. With up to 120 slots this avoids thousands of native calls per second
            // for slots that are only placeholders.
            if (slot.IsEmpty) continue;

            slot.Update();

            ApplyIconFallback(slot);
            ApplyCooldownFallback(slot);
        }

        // A locked bar only accepts contents into slots that are still empty, so the state has to follow
        // the slot contents.
        ApplySlotInteraction();

        if (_dirtySlots.Count == 0) return;

        int[] dirty = [.. _dirtySlots];

        _dirtySlots.Clear();

        OnSlotsChanged?.Invoke(BarId, dirty);
    }

    /// <summary>
    /// Replaces the icon of a substituted action with the icon of the action that is actually shown.
    /// </summary>
    /// <remarks>
    /// Some actions are replaced by another action depending on the current state, for example the dancer
    /// weapon skills that turn into the currently available step while dancing. The native slot render
    /// keeps the icon of the stored command while the tooltip already reports the substitution, so the
    /// icon is resolved from the apparent action id instead.
    /// </remarks>
    private static void ApplyIconFallback(HotbarNode slot) {
        if (slot.ApparentSlotType is not HotbarSlotType.Action) return;

        HotbarSlotContent? content = slot.GetSlotContent();
        if (content is not { Type: HotbarSlotType.Action } command) return;

        uint apparent = slot.ApparentActionId;
        if (apparent == 0 || apparent == command.Id) return;

        uint icon = GetActionIcon(apparent);
        if (icon != 0 && slot.IconId != icon) slot.IconId = icon;
    }

    private static uint GetActionIcon(uint actionId) {
        if (ActionIcons.TryGetValue(actionId, out uint cached)) return cached;

        var  action = Framework.Service<IDataManager>().GetExcelSheet<ActionSheet>().FindRow(actionId);
        uint icon   = action is null ? 0 : (uint) action.Value.Icon;

        ActionIcons[actionId] = icon;

        return icon;
    }

    private static readonly Dictionary<uint, uint> ActionIcons = [];

    /// <summary>
    /// Draws the cooldown radial and the remaining seconds of an action slot when the native slot render
    /// did not provide them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The radial is the same game texture atlas that the native hotbar and QoLBarEx use, so the look is
    /// identical: a dark fan that shrinks as the recast runs out, with the remaining seconds centered.
    /// </para>
    /// <para>
    /// Slots allocated by hand are not part of the game's hotbar array, so
    /// <c>PrepareSlotForRender</c> can leave the cooldown at zero. QoLBarEx reads the recast straight from
    /// <see cref="ActionManager"/>; this does the same, but only as a fallback so that a cooldown the game
    /// did report (which already includes the GCD) is never replaced.
    /// </para>
    /// </remarks>
    private static unsafe void ApplyCooldownFallback(HotbarNode slot) {
        if (slot.IconNode.CooldownPercentVisible || slot.IconNode.CooldownSecondsVisible) return;

        HotbarSlotContent? content = slot.GetSlotContent();
        if (content is not { Type: HotbarSlotType.Action } action) return;

        ActionManager* actionManager = ActionManager.Instance();
        if (actionManager is null) return;

        float max = actionManager->GetRecastTime(ActionType.Action, action.Id);
        if (max <= 0) return;

        float elapsed   = actionManager->GetRecastTimeElapsed(ActionType.Action, action.Id);
        float remaining = max - elapsed;

        if (remaining <= 0) return;

        // Percent is the remaining share: one is a full overlay, zero is ready again.
        slot.IconNode.CooldownPercent        = Math.Clamp(remaining / max, 0.0f, 1.0f);
        slot.IconNode.CooldownPercentVisible = true;
        slot.IconNode.CooldownSeconds        = (uint) Math.Ceiling(remaining);
        slot.IconNode.CooldownSecondsVisible = true;
    }

    /// <summary>
    /// Executes the contents of the given slot through the game's own hotbar module.
    /// </summary>
    /// <remarks>
    /// The slot's <c>KeyBind</c> is only used to draw the key label. Its key press is handled by the
    /// manager, which applies the same focus and edge-detection rules to every bar.
    /// </remarks>
    public void ExecuteSlot(int index) {
        if (index < 0 || index >= _slots.Count) return;

        _slots[index].OnClicked?.Invoke(_slots[index]);
    }

    /// <inheritdoc />
    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan) {
        ConfigureWindow();

        // KTK normally makes the window header the addon's focus node. With the header hidden that
        // would leave a focusable, invisible node in the game's keyboard focus chain, which makes the
        // game swallow every key. Use the plain, non-focusable root node instead.
        if (WindowNode is WindowNode window) {
            window.WindowHeaderFocusNode.RemoveNodeFlags(NodeFlags.Focusable);
        }

        addon->FocusNode = RootNode;

        // The game destroys the entire node tree of an addon when it is closed, so the nodes have to be
        // created again every time the window is opened.
        _rootNode = new VerticalListNode {
            Position         = ContentStartPosition,
            Size             = ContentSize,
            ItemSpacing      = SlotSpacing,
            FirstItemSpacing = 0.0f,
            Anchor           = VerticalListAnchor.Top,
            NavIndex         = 1,
        };
        _rootNode.AttachNode(this);

        // Reopening a bar must not show it again while its display condition is still not met. This also
        // re-applies the visible state, so a bar that is hidden at the moment the addon is rebuilt can
        // still be shown again by SetContentVisible(true).
        _nodesValid = true;
        ApplyContentVisibility();

        ResizeWindow();
        Rebuild();
        ApplySlots();

        base.OnSetup(addon, atkValueSpan);
    }

    /// <inheritdoc />
    protected override void OnHide(AtkUnitBase* addon) {
        // The nodes are about to be destroyed by the game, so drop all references to them and stop
        // writing to them until OnSetup has rebuilt the tree.
        _nodesValid = false;
        _rootNode   = null;
        _dirtySlots.Clear();
        _rows.Clear();
        _slots.Clear();
    }

    /// <summary>
    /// Turns the window into a bare frame without a title, a subtitle and a close button.
    /// </summary>
    private void ConfigureWindow() {
        if (WindowNode is not WindowNode window) return;

        window.ShowCloseButton            = false;
        window.ShowConfigButton           = false;
        window.ShowHelpButton             = false;
        window.TitleNode.IsVisible        = false;
        window.SubtitleNode.IsVisible     = false;
        window.DividingLineNode.IsVisible = false;

        // Hiding the header container also removes the strip at the top of the window that would
        // otherwise swallow mouse clicks.
        window.HeaderContainerNode.IsVisible   = false;
        window.HeaderCollisionNode.IsVisible   = false;
        window.HeaderContainerNode.Height      = 0.0f;

        // The bar is positioned by its configuration, so the window itself must not react to the mouse.
        // Otherwise holding the left button on a gap between slots drags the whole bar around.
        window.CollisionNode.RemoveNodeFlags(
            NodeFlags.RespondToMouse | NodeFlags.Focusable | NodeFlags.HasCollision
        );

        ApplyBackgroundVisibility();
    }

    /// <summary>
    /// Shows or hides the frame of the window.
    /// </summary>
    private void ApplyBackgroundVisibility() {
        if (WindowNode is not WindowNode window) return;

        window.BackgroundTextureNode.IsVisible = _showBackground;
        window.BackgroundImageNode.IsVisible   = _showBackground;
        window.BorderTextureNode.IsVisible     = _showBackground;
    }

    private void ResizeWindow() {
        Vector2 size = GetWindowSize(_slotCount, _slotsPerRow);

        SetWindowSize(size);

        if (_rootNode is not null) {
            _rootNode.Position = ContentStartPosition;
            _rootNode.Size     = ContentSize;
        }
    }

    private void Rebuild() {
        if (_rootNode is null) return;

        foreach (HorizontalListNode row in _rows) {
            _rootNode.RemoveNode(row);
        }

        _rows.Clear();
        _slots.Clear();

        int   columns = Math.Min(_slotCount, _slotsPerRow);
        int   rows    = (int) Math.Ceiling(_slotCount / (double) columns);
        float width   = ContentSize.X;

        for (int r = 0; r < rows; r++) {
            HorizontalListNode rowNode = new() {
                ItemSpacing = SlotSpacing,
                Alignment   = HorizontalListAnchor.Left,

                // The height of a row is what the vertical list uses to place the next row, so it has to
                // match the height of a slot.
                Size = new Vector2(width, SlotHeight),
            };
            _rootNode.AddNode(rowNode);
            _rows.Add(rowNode);

            int start = r * columns;
            int end   = Math.Min(start + columns, _slotCount);

            for (int i = start; i < end; i++) {
                HotbarNode slot = new() {
                    Size = new Vector2(SlotWidth, SlotHeight),

                    // Empty slots have to stay visible and droppable, otherwise there would be nowhere to
                    // drop an action or an item into.
                    IsBackgroundShown = true,
                    IsClickable       = true,
                    IsDraggable       = true,

                    // Accept anything the game can drag around: actions, items, macros and crystals.
                    AcceptedType = DragDropType.Everything,

                    // The manager handles key presses for all bars with proper focus checks and edge
                    // detection. KTK must only draw the key label on the slot.
                    HandleKeyBind = false,
                };

                // Content changes are reported per slot, so that only the slots that were actually edited
                // are re-read. KTK's own handler runs first and updates the slot data.
                int slotIndex = i;
                slot.OnPayloadAccepted += (_, _) => _dirtySlots.Add(slotIndex);
                slot.OnDiscard        += _ => _dirtySlots.Add(slotIndex);

                // A bar must never take keyboard or gamepad focus. A focusable collision node lets the
                // game route the escape key to the addon, which closes and rebuilds it (the flicker).
                slot.CollisionNode.RemoveNodeFlags(NodeFlags.Focusable);

                // The empty-slot background is a fixed 44x44 drag target, while the icon part is 44x48 and
                // draws a frame around its content. Stretching the background to the slot size makes an
                // empty slot look the same size as a filled one.
                slot.DragDropBackgroundNode.Size     = new Vector2(SlotWidth, SlotHeight);
                slot.DragDropBackgroundNode.Position = Vector2.Zero;
                slot.IconNode.Size                   = new Vector2(SlotWidth, SlotHeight);

                rowNode.AddNode(slot);

                _slots.Add(slot);
            }
        }

        _rootNode.RecalculateLayout();
    }

    private void ApplySlots() {
        for (int i = 0; i < _slots.Count; i++) {
            HotbarNode        slot    = _slots[i];
            CustomHotbarSlot content = i < _pendingSlots.Count ? _pendingSlots[i] : new CustomHotbarSlot();

            // An empty slot has to stay empty even when it still carries the native type of a previous
            // content: older builds left RawType behind when the content was dragged out, and restoring a
            // type with an id of zero produces a slot the icon component draws as a black box.
            if (content.Kind is CustomHotbarSlotKind.Empty) {
                slot.ClearSlot();
            }

            // The stored native type decides how a slot is restored. Falling back to the kind would turn an
            // event item into a regular item, which is looked up in a different sheet and therefore
            // renders as a black box.
            else if (content.RawType is not HotbarSlotType.Empty) {
                switch (content.RawType) {
                    case HotbarSlotType.Action:
                        slot.SetAction(content.Id);
                        break;

                    case HotbarSlotType.Macro:
                        slot.SetSlotMacro(content.Id);
                        break;

                    // While an item is dragged around it is addressed by its inventory slot, which is not
                    // stable across sessions. Storing it as a plain item is what the game itself does when
                    // it saves a hotbar.
                    case HotbarSlotType.InventoryItem:
                        slot.SetSlotItem(content.Id);
                        break;

                    default:
                        slot.SetSlot(content.RawType, content.Id);
                        break;
                }
            }
            else {
                // Configurations written before the native type was stored only carry the kind.
                switch (content.Kind) {
                    case CustomHotbarSlotKind.Action:
                        slot.SetAction(content.Id);
                        break;

                    case CustomHotbarSlotKind.Item:
                        slot.SetSlotItem(content.Id);
                        break;

                    case CustomHotbarSlotKind.Macro:
                        slot.SetSlotMacro(content.Id);
                        break;

                    case CustomHotbarSlotKind.Crystal:
                        slot.SetSlot(HotbarSlotType.Crystal, content.Id);
                        break;

                    case CustomHotbarSlotKind.KeyItem:
                        slot.SetSlot(HotbarSlotType.KeyItem, content.Id);
                        break;

                    default:
                        slot.ClearSlot();
                        break;
                }
            }

            slot.KeyBind = CreateKeyBind(content.Hotkey, content.HotkeyModifier);

            slot.Update();
        }

        // Applying the stored configuration is not a user edit.
        _dirtySlots.Clear();

        ApplySlotInteraction();
    }

    /// <summary>
    /// Hides or shows the frame of the slots that have no content yet.
    /// </summary>
    /// <remarks>
    /// An empty slot keeps occupying its place in the layout; only its drag target background is hidden,
    /// so the bar looks like a group of icons with gaps instead of a grid of empty frames.
    /// </remarks>
    public void SetHideEmptySlots(bool hide) {
        if (hide == _hideEmptySlots) return;

        _hideEmptySlots = hide;

        ApplySlotInteraction();
    }

    /// <summary>
    /// Applies the interaction state of every slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty slot is not clickable: nothing happens when it is clicked, and a clickable node would take
    /// pointer focus, turn the cursor into the hand cursor and let escape cancel it, which is the flicker.
    /// </para>
    /// <para>
    /// A locked bar still accepts contents into empty slots, but non-empty slots reject drops so that
    /// their contents cannot be moved to another slot, and no slot can be dragged out of the bar.
    /// </para>
    /// </remarks>
    private void ApplySlotInteraction() {
        for (int i = 0; i < _slots.Count; i++) {
            HotbarNode slot    = _slots[i];
            bool       isEmpty = slot.IsEmpty;

            bool clickable = !isEmpty;
            if (slot.IsClickable != clickable) slot.IsClickable = clickable;

            bool showFrame = !_hideEmptySlots || !isEmpty;
            if (slot.DragDropBackgroundNode.IsVisible != showFrame) slot.DragDropBackgroundNode.IsVisible = showFrame;

            bool draggable = !_locked;
            if (slot.IsDraggable != draggable) slot.IsDraggable = draggable;

            DragDropType accepted = !_locked || isEmpty ? DragDropType.Everything : DragDropType.Nothing;
            if (slot.AcceptedType != accepted) slot.AcceptedType = accepted;
        }
    }

    /// <summary>
    /// Creates the native key setting for a slot.
    /// </summary>
    private static KeySetting? CreateKeyBind(VirtualKey key, KeyModifierFlag modifier) {
        if (key == VirtualKey.NO_KEY) return null;

        return new KeySetting {
            Key         = (SeVirtualKey) key,
            KeyModifier = modifier,
        };
    }

    /// <summary>
    /// Reads the content of a native hotbar slot into the given descriptor.
    /// </summary>
    private static void ReadSlot(HotbarSlotContent? content, CustomHotbarSlot target) {
        if (content is null) {
            target.Kind    = CustomHotbarSlotKind.Empty;
            target.RawType = HotbarSlotType.Empty;
            target.Id      = 0;

            return;
        }

        HotbarSlotContent value = content.Value;

        target.RawType = value.Type;
        target.Id      = value.Id;

        target.Kind = value.Type switch {
            HotbarSlotType.Action => CustomHotbarSlotKind.Action,
            HotbarSlotType.Item
                or HotbarSlotType.InventoryItem
                or HotbarSlotType.EventItem => CustomHotbarSlotKind.Item,
            HotbarSlotType.Crystal => CustomHotbarSlotKind.Crystal,
            HotbarSlotType.KeyItem => CustomHotbarSlotKind.KeyItem,
            HotbarSlotType.Macro   => CustomHotbarSlotKind.Macro,

            // Anything else (main commands, general actions, mounts, ...) is kept verbatim through
            // RawType instead of being dropped as empty.
            _ => CustomHotbarSlotKind.Other,
        };
    }
}
