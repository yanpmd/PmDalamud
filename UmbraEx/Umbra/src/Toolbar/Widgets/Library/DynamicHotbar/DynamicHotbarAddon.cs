using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using HotbarSlotType = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.HotbarSlotType;

namespace Umbra.Widgets.Library.DynamicHotbar;

/// <summary>
/// A native game window that shows a grid of hotbar slots.
/// </summary>
/// <remarks>
/// The slots are built on top of <see cref="HotbarNode"/>, which provides the native drag and drop
/// behaviour of the game as well as the native appearance of hotbar slots (cooldowns, costs, charges,
/// macro icons, ...). Clicking a slot executes it through the game's own hotbar module, which means
/// actions are cast, items are used and macros are run.
/// </remarks>
internal sealed unsafe class DynamicHotbarAddon : NativeAddon {
    private const int   MaxSlotsPerRow = 10;
    private const float SlotWidth      = 44.0f;
    private const float SlotHeight     = 48.0f;
    private const float SlotSpacing    = 4.0f;

    // The content area of a window is the window size minus the frame, the header and the content padding.
    private const float HorizontalFrameOverhead = 24.0f;
    private const float VerticalFrameOverhead   = 62.0f;

    private readonly List<HotbarNode>        _slots      = [];
    private readonly List<DynamicHotbarSlot> _knownSlots = [];
    private readonly List<HorizontalListNode> _rows       = [];

    private VerticalListNode?       _rootNode;
    private List<DynamicHotbarSlot> _pendingSlots = [];
    private int                     _slotCount    = 8;

    /// <summary>
    /// Raised when the content of one of the slots has been changed.
    /// </summary>
    public event Action? OnSlotsChanged;

    /// <summary>
    /// Raised when the window has been closed. The nodes of the window are still alive at this point,
    /// but they are destroyed by the game right afterwards.
    /// </summary>
    public event Action? OnClosed;

    /// <summary>
    /// The number of slots that are currently present in this window.
    /// </summary>
    public int SlotCount => _slots.Count;

    /// <summary>
    /// Calculates the size of the window that is required to show the given amount of slots.
    /// </summary>
    public static Vector2 GetWindowSize(int slotCount) {
        slotCount = Math.Clamp(slotCount, 1, 32);

        int columns = Math.Min(slotCount, MaxSlotsPerRow);
        int rows    = (int) Math.Ceiling(slotCount / (double) columns);

        float contentWidth  = columns * SlotWidth + (columns - 1) * SlotSpacing;
        float contentHeight = rows * SlotHeight + (rows - 1) * SlotSpacing;

        return new Vector2(contentWidth + HorizontalFrameOverhead, contentHeight + VerticalFrameOverhead);
    }

    /// <summary>
    /// Changes the number of slots in this window.
    /// </summary>
    public void SetSlotCount(int count) {
        count = Math.Clamp(count, 1, 32);

        if (count == _slotCount) return;

        _slotCount = count;

        if (_rootNode is null) return;

        ResizeWindow();
        Rebuild();
        ApplySlots();
    }

    /// <summary>
    /// Writes the given slots into the window.
    /// </summary>
    public void LoadSlots(IReadOnlyList<DynamicHotbarSlot> slots) {
        _pendingSlots = new List<DynamicHotbarSlot>(slots);

        if (_rootNode is null) return;

        ApplySlots();
    }

    /// <summary>
    /// Reads the current contents of all slots of this window.
    /// </summary>
    public List<DynamicHotbarSlot> CaptureSlots() {
        List<DynamicHotbarSlot> slots = new(_slots.Count);

        foreach (HotbarNode slot in _slots) {
            slots.Add(ToSlot(slot.GetSlotContent()));
        }

        return slots;
    }

    /// <summary>
    /// Sets the subtitle of the window.
    /// </summary>
    /// <remarks>
    /// The window node of an addon is destroyed by the game once the addon has been closed, so the
    /// subtitle may only be changed while the window is actually open.
    /// </remarks>
    public void SetSubtitle(string subtitle) {
        if (!IsOpen) return;

        Subtitle = subtitle;
    }

    /// <inheritdoc />
    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan) {
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

        ResizeWindow();
        Rebuild();
        ApplySlots();

        base.OnSetup(addon, atkValueSpan);
    }

    /// <inheritdoc />
    protected override void OnDraw(AtkUnitBase* addon) {
        bool changed = false;

        for (int i = 0; i < _slots.Count; i++) {
            _slots[i].Update();

            DynamicHotbarSlot current = ToSlot(_slots[i].GetSlotContent());
            if (current == _knownSlots[i]) continue;

            _knownSlots[i] = current;
            changed         = true;
        }

        if (changed) OnSlotsChanged?.Invoke();

        ReportDragState();
    }

    /// <summary>
    /// Reports whether the game is currently dragging something around, which is needed to tell the
    /// difference between a drag that never reaches the slots and a drag that is not started at all.
    /// </summary>
    private void ReportDragState() {
        var  manager  = AtkStage.Instance()->DragDropManager;
        bool dragging = manager.IsDragging;

        if (dragging && !_wasDragging) {
            var dragDrop = manager.DragDrop1;

            Logger.Info(
                "[DynamicHotbar] a drag operation started: " +
                $"type={(dragDrop is null ? "unknown" : dragDrop->DragDropType.ToString())} " +
                $"refIndex={(dragDrop is null ? -1 : dragDrop->DragDropReferenceIndex.ToString())}"
            );
        }

        _wasDragging = dragging;
    }

    private bool _wasDragging;

    /// <inheritdoc />
    protected override void OnHide(AtkUnitBase* addon) {
        // The nodes are about to be destroyed by the game, so drop all references to them. The content of
        // the slots is still readable at this point, which is why the closed event is raised first.
        OnClosed?.Invoke();

        _rootNode = null;
        _rows.Clear();
        _slots.Clear();
        _knownSlots.Clear();
    }

    /// <summary>
    /// Attaches logging to a slot so that problems with the native drag and drop can be diagnosed.
    /// </summary>
    private static void AttachDiagnostics(HotbarNode slot, int index) {
        Action<DragDropNode, DragDropPayload>? inner = slot.OnPayloadAccepted;

        slot.OnPayloadAccepted = (node, payload) => {
            Logger.Info(
                $"[DynamicHotbar] slot {index} accepted payload: type={payload.Type} int1={payload.Int1} " +
                $"int2={payload.Int2} ref={payload.ReferenceIndex}"
            );

            inner?.Invoke(node, payload);
        };

        slot.CollisionNode.AddEvent(AtkEventType.MouseOver, () => {
            // Only log the first hover of a slot, otherwise the log is flooded while the mouse moves.
            if (!_hoveredSlots.Add(index)) return;

            Logger.Info($"[DynamicHotbar] slot {index} hovered");
        });
    }

    private static readonly HashSet<int> _hoveredSlots = [];

    private void ResizeWindow() {
        Vector2 size = GetWindowSize(_slotCount);
        if (size == Size) return;

        SetWindowSize(size);

        if (_rootNode is not null) _rootNode.Size = ContentSize;
    }

    private void Rebuild() {
        if (_rootNode is null) return;

        foreach (HorizontalListNode row in _rows) {
            _rootNode.RemoveNode(row);
        }

        _rows.Clear();
        _slots.Clear();
        _knownSlots.Clear();

        int   columns = Math.Min(_slotCount, MaxSlotsPerRow);
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
                };
                rowNode.AddNode(slot);

                AttachDiagnostics(slot, i);

                _slots.Add(slot);
                _knownSlots.Add(DynamicHotbarSlot.Empty);
            }
        }

        _rootNode.RecalculateLayout();
    }

    private void ApplySlots() {
        for (int i = 0; i < _slots.Count; i++) {
            HotbarNode        slot    = _slots[i];
            DynamicHotbarSlot content = i < _pendingSlots.Count ? _pendingSlots[i] : DynamicHotbarSlot.Empty;

            switch (content.Kind) {
                case DynamicHotbarSlotKind.Action:
                    slot.SetAction(content.Id);
                    break;

                case DynamicHotbarSlotKind.Item:
                    slot.SetSlotItem(content.Id);
                    break;

                case DynamicHotbarSlotKind.Macro:
                    slot.SetSlotMacro(content.Id);
                    break;

                default:
                    slot.ClearSlot();
                    break;
            }

            slot.Update();
            _knownSlots[i] = content;
        }
    }

    private static DynamicHotbarSlot ToSlot(HotbarSlotContent? content) {
        if (content is null) return DynamicHotbarSlot.Empty;

        HotbarSlotContent value = content.Value;

        return value.Type switch {
            HotbarSlotType.Action => new DynamicHotbarSlot(DynamicHotbarSlotKind.Action, value.Id),

            HotbarSlotType.Item or HotbarSlotType.InventoryItem or HotbarSlotType.EventItem =>
                new DynamicHotbarSlot(DynamicHotbarSlotKind.Item, value.Id),

            HotbarSlotType.Macro => new DynamicHotbarSlot(DynamicHotbarSlotKind.Macro, value.Id),

            _ => DynamicHotbarSlot.Empty,
        };
    }
}
