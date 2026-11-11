using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace Umbra.CustomHotbar;

/// <summary>
/// The kind of content that is stored in a custom hotbar slot.
/// </summary>
public enum CustomHotbarSlotKind : byte {
    /// <summary>The slot is empty.</summary>
    Empty = 0,

    /// <summary>The slot holds a player action, addressed by its action id.</summary>
    Action = 1,

    /// <summary>The slot holds an item, addressed by its item id.</summary>
    Item = 2,

    /// <summary>The slot holds a macro, addressed by its index in the macro list.</summary>
    Macro = 3,
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
    /// The id of the content, interpreted according to <see cref="Kind"/>.
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
    /// Creates a copy of this slot.
    /// </summary>
    public CustomHotbarSlot Clone() {
        return new CustomHotbarSlot {
            Kind           = Kind,
            Id             = Id,
            Hotkey         = Hotkey,
            HotkeyModifier = HotkeyModifier,
            KeyPassthrough = KeyPassthrough,
        };
    }
}
