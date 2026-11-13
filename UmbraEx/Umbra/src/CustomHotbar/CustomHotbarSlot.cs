using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using HotbarSlotType = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.HotbarSlotType;

namespace Umbra.CustomHotbar;

/// <summary>
/// The kind of content that is stored in a custom hotbar slot.
/// </summary>
/// <remarks>
/// This is only used to describe and group the content. The exact native slot type is stored separately in
/// <see cref="CustomHotbarSlot.RawType"/>, so that slot types without a dedicated kind here (main commands,
/// general actions, mounts, ...) can still be saved and restored.
/// </remarks>
public enum CustomHotbarSlotKind : byte {
    /// <summary>The slot is empty.</summary>
    Empty = 0,

    /// <summary>The slot holds a player action, addressed by its action id.</summary>
    Action = 1,

    /// <summary>The slot holds an item, addressed by its item id.</summary>
    Item = 2,

    /// <summary>The slot holds a macro, addressed by its index in the macro list.</summary>
    Macro = 3,

    /// <summary>The slot holds a crystal, addressed by its item id.</summary>
    Crystal = 4,

    /// <summary>The slot holds a key item, addressed by its key item id.</summary>
    KeyItem = 5,

    /// <summary>The slot holds any other native slot type, see <see cref="CustomHotbarSlot.RawType"/>.</summary>
    Other = 6,
}

/// <summary>
/// A single slot of the custom hotbar.
/// </summary>
[Serializable]
public class CustomHotbarSlot {
    /// <summary>
    /// The kind of content in this slot.
    /// </summary>
    public CustomHotbarSlotKind Kind { get; set; } = CustomHotbarSlotKind.Empty;

    /// <summary>
    /// The native slot type this slot was created from.
    /// </summary>
    /// <remarks>
    /// Stored so that slot types that have no dedicated <see cref="CustomHotbarSlotKind"/> can be restored
    /// exactly. <see cref="HotbarSlotType.Empty"/> means that the slot predates this field, in which case
    /// the kind alone decides how it is restored.
    /// </remarks>
    public HotbarSlotType RawType { get; set; } = HotbarSlotType.Empty;

    /// <summary>
    /// The id of the content, interpreted according to <see cref="Kind"/> and <see cref="RawType"/>.
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// The key that triggers this slot. <see cref="VirtualKey.NO_KEY"/> means that no key is bound.
    /// </summary>
    public VirtualKey Hotkey { get; set; } = VirtualKey.NO_KEY;

    /// <summary>
    /// The modifier key that must be held down while the hotkey is pressed.
    /// </summary>
    public KeyModifierFlag HotkeyModifier { get; set; } = KeyModifierFlag.None;

    /// <summary>
    /// Whether the bound key is left for the game to handle as well. When false, the key is consumed on
    /// the single frame the hotkey fires.
    /// </summary>
    public bool KeyPassthrough { get; set; }

    /// <summary>
    /// True when a key is bound to this slot.
    /// </summary>
    public bool HasHotkey => Hotkey != VirtualKey.NO_KEY;

    /// <summary>
    /// Clears the content of this slot while keeping its key binding.
    /// </summary>
    /// <remarks>
    /// <see cref="RawType"/> must be cleared as well: a slot whose content was dragged out still carries
    /// the native type it had, and restoring a type with an id of zero produces an unusable slot that the
    /// icon component draws as a black box.
    /// </remarks>
    public void ClearContent() {
        Kind    = CustomHotbarSlotKind.Empty;
        RawType = HotbarSlotType.Empty;
        Id      = 0;
    }

    /// <summary>
    /// Creates a copy of this slot.
    /// </summary>
    public CustomHotbarSlot Clone() {
        return new CustomHotbarSlot {
            Kind           = Kind,
            RawType        = RawType,
            Id             = Id,
            Hotkey         = Hotkey,
            HotkeyModifier = HotkeyModifier,
            KeyPassthrough = KeyPassthrough,
        };
    }
}
