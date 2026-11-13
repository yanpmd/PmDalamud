using System;

namespace Umbra.CustomHotbar;

/// <summary>
/// The configuration of one custom hotbar instance.
/// </summary>
/// <remarks>
/// Every bar owns its own slots, key bindings, position and visibility condition. None of the bars is
/// partitioned by zone.
/// </remarks>
[Serializable]
public class CustomHotbarConfig {
    /// <summary>
    /// The stable identity of this bar. It is also used to derive the name of its native addon.
    /// </summary>
    public string Id { get; set; } = $"hotbar{Guid.NewGuid():N}";

    /// <summary>
    /// The name shown in the settings window.
    /// </summary>
    public string Name { get; set; } = "Hotbar";

    /// <summary>
    /// Whether this bar is enabled at all.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the background of the bar is drawn. When disabled, the bar consists of nothing but its
    /// slots.
    /// </summary>
    public bool ShowBackground { get; set; } = true;

    /// <summary>
    /// Whether the bar hides itself while the mouse cursor is not near it.
    /// </summary>
    public bool EnableAutoHide { get; set; }

    /// <summary>
    /// The number of slots of the bar.
    /// </summary>
    public int SlotCount { get; set; } = 8;

    /// <summary>
    /// The number of slots that are shown in one row.
    /// </summary>
    public int SlotsPerRow { get; set; } = CustomHotbarAddon.DefaultSlotsPerRow;

    /// <summary>
    /// Whether the slot layout of this bar is locked.
    /// </summary>
    /// <remarks>
    /// A locked bar still accepts contents into empty slots, but existing contents cannot be moved to
    /// another slot or dragged out of the bar.
    /// </remarks>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Whether the frame of empty slots is hidden.
    /// </summary>
    /// <remarks>
    /// Empty slots keep occupying their place in the layout either way; only their drag target background
    /// is hidden, so the bar shows icons with gaps instead of a grid of empty frames.
    /// </remarks>
    public bool HideEmptySlots { get; set; }

    /// <summary>
    /// The horizontal offset of the bar, in UI units, relative to the horizontal anchor.
    /// </summary>
    public int XPos { get; set; }

    /// <summary>
    /// The vertical offset of the bar, in UI units, relative to the vertical anchor.
    /// </summary>
    public int YPos { get; set; } = 80;

    /// <summary>
    /// The horizontal anchor of the bar. One of Left, Center or Right.
    /// </summary>
    public string XAlign { get; set; } = "Center";

    /// <summary>
    /// The vertical anchor of the bar. One of Top, Center or Bottom.
    /// </summary>
    public string YAlign { get; set; } = "Bottom";

    /// <summary>
    /// The id of the condition set that controls the visibility of the bar. An empty value means that
    /// the bar is always visible.
    /// </summary>
    public string ConditionSetId { get; set; } = string.Empty;

    /// <summary>
    /// The contents of the slots of the bar.
    /// </summary>
    public List<CustomHotbarSlot> Slots { get; set; } = [];

    /// <summary>
    /// Makes sure the configuration contains a valid identity and a valid amount of slots.
    /// </summary>
    public void Normalize() {
        if (string.IsNullOrWhiteSpace(Id)) Id = $"hotbar{Guid.NewGuid():N}";
        if (string.IsNullOrWhiteSpace(Name)) Name = "Hotbar";

        XAlign ??= "Center";
        YAlign ??= "Bottom";
        ConditionSetId ??= string.Empty;
        Slots ??= [];

        SlotCount   = Math.Clamp(SlotCount, 1, CustomHotbarAddon.MaxSlots);
        SlotsPerRow = Math.Clamp(SlotsPerRow, 1, CustomHotbarAddon.MaxSlotsPerRow);

        while (Slots.Count < SlotCount) {
            Slots.Add(new CustomHotbarSlot());
        }

        // Repairs configurations written by builds that cleared the kind but kept the native type when a
        // slot was emptied. Such a slot is restored with an id of zero and renders as a black box.
        foreach (CustomHotbarSlot slot in Slots) {
            if (slot.Kind is CustomHotbarSlotKind.Empty) slot.ClearContent();
        }

        while (Slots.Count > CustomHotbarAddon.MaxSlots) {
            Slots.RemoveAt(Slots.Count - 1);
        }
    }

    /// <summary>
    /// Copies the values of the given configuration into this one, while keeping the identity of this
    /// instance intact.
    /// </summary>
    /// <remarks>
    /// The list of slots is replaced instead of being merged, because the slots are always written as a
    /// whole by whoever changes them.
    /// </remarks>
    public void Update(CustomHotbarConfig other) {
        IsEnabled      = other.IsEnabled;
        Name           = other.Name;
        ShowBackground = other.ShowBackground;
        EnableAutoHide = other.EnableAutoHide;
        SlotCount      = other.SlotCount;
        SlotsPerRow    = other.SlotsPerRow;
        IsLocked       = other.IsLocked;
        HideEmptySlots = other.HideEmptySlots;
        XPos           = other.XPos;
        YPos           = other.YPos;
        XAlign         = other.XAlign;
        YAlign         = other.YAlign;
        ConditionSetId = other.ConditionSetId;

        Slots.Clear();

        foreach (CustomHotbarSlot slot in other.Slots) {
            Slots.Add(slot.Clone());
        }

        Normalize();
    }

    /// <summary>
    /// Creates a deep copy of this configuration, with a new identity.
    /// </summary>
    public CustomHotbarConfig Clone(string? newId = null, string? newName = null) {
        var copy = new CustomHotbarConfig {
            Id             = newId ?? $"hotbar{Guid.NewGuid():N}",
            Name           = newName ?? Name,
            IsEnabled      = IsEnabled,
            ShowBackground = ShowBackground,
            EnableAutoHide = EnableAutoHide,
            SlotCount      = SlotCount,
            SlotsPerRow    = SlotsPerRow,
            IsLocked       = IsLocked,
            HideEmptySlots = HideEmptySlots,
            XPos           = XPos,
            YPos           = YPos,
            XAlign         = XAlign,
            YAlign         = YAlign,
            ConditionSetId = ConditionSetId,
        };

        foreach (CustomHotbarSlot slot in Slots) {
            copy.Slots.Add(slot.Clone());
        }

        copy.Normalize();

        return copy;
    }
}
