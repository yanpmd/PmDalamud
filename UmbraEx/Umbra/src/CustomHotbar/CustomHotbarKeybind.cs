using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Umbra.CustomHotbar;

/// <summary>
/// Global hotkey handling for the custom hotbars, modelled after QoLBarEx.
/// </summary>
/// <remarks>
/// <para>
/// Key presses are read from the Win32 keyboard state instead of the game's input buffer or ImGui's
/// input capture. Every frame the state of all 256 virtual keys is turned into edge flags, and the
/// registrations that were collected during the previous frame are consumed exactly once.
/// </para>
/// <para>
/// Nothing writes to <see cref="IKeyState"/> unless a hotkey actually fired and the slot opted out of
/// key passthrough. Holding a key never repeats an action, and no setting window rebuild happens on
/// this path.
/// </para>
/// </remarks>
internal static unsafe class CustomHotbarKeybind {
    private const int ModifierMask = -1 << 16;
    private const int ShiftBit     = 1 << 16;
    private const int CtrlBit      = 1 << 17;
    private const int AltBit       = 1 << 18;
    private const int EscapeKey    = 0x1B;

    [Flags]
    private enum KeyFlags {
        None      = 0,
        Held      = 1,
        KeyDown   = 2,
        KeyUp     = 4,
        ShortHold = 8,
    }

    /// <summary>
    /// The per-key edge state machine. KeyDown and KeyUp are each set for a single frame only.
    /// </summary>
    private struct KeyState {
        public KeyFlags Flags;
        public float    HoldDuration;
        public bool     WasShortHeld;

        public void Update(bool down) {
            if (down) {
                KeyFlags previous = Flags;

                Flags = KeyFlags.Held;

                if ((previous & KeyFlags.Held) == 0) {
                    Flags |= KeyFlags.KeyDown;
                }
                else if (HoldDuration >= 0.2f) {
                    Flags |= KeyFlags.ShortHold;
                }

                HoldDuration += (float) Framework.DalamudFramework.UpdateDelta.TotalSeconds;

                return;
            }

            if (Flags == KeyFlags.None) return;

            WasShortHeld = (Flags & KeyFlags.ShortHold) != 0;
            Flags        = Flags != KeyFlags.KeyUp ? KeyFlags.KeyUp : KeyFlags.None;
            HoldDuration = 0;
        }
    }

    private readonly record struct Registration(string BarId, int SlotIndex, int Hotkey, bool Passthrough);

    private static readonly byte[]          KeyboardState = new byte[256];
    private static readonly KeyState[]      KeyStates     = new KeyState[256];
    private static readonly List<Registration> Registrations = [];

    /// <summary>
    /// Set by a focused hotkey input during the draw phase. It suppresses the next consume pass, so
    /// typing a key into the binding editor does not also fire an already bound slot.
    /// </summary>
    public static bool CaptureActive { get; set; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] state);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(nint handle, out int processId);

    /// <summary>
    /// Whether hotkeys must be skipped entirely this frame.
    /// </summary>
    /// <remarks>
    /// <c>io.WantCaptureKeyboard</c> is deliberately not used here: Umbra keeps ImGui windows alive
    /// permanently, which makes that value true for long periods and would stop hotkeys from working
    /// until the player clicks the game world to take focus back.
    /// </remarks>
    public static bool Disabled {
        get {
            RaptureAtkModule* module = RaptureAtkModule.Instance();

            // Chat box, macro editor or any other native text input has priority.
            if (module != null && module->IsTextInputActive()) return true;

            // Only act on keys that went to the game process.
            nint foreground = GetForegroundWindow();
            if (foreground == nint.Zero) return true;

            GetWindowThreadProcessId(foreground, out int processId);

            return processId != Environment.ProcessId;
        }
    }

    /// <summary>
    /// Samples the keyboard and updates the edge state machine. Call once per framework tick.
    /// </summary>
    public static void Tick() {
        if (!GetKeyboardState(KeyboardState)) return;

        for (int i = 0; i < KeyStates.Length; i++) {
            KeyStates[i].Update((KeyboardState[i] & 0x80) != 0);
        }
    }

    /// <summary>
    /// Registers a slot for the next frame. Only visible (enabled) bars are registered.
    /// </summary>
    public static void Register(string barId, int slotIndex, int hotkey, bool passthrough) {
        if (hotkey == 0) return;

        Registrations.Add(new Registration(barId, slotIndex, hotkey, passthrough));
    }

    /// <summary>
    /// Consumes the registrations of the previous frame and clears the list, just like QoLBarEx does.
    /// </summary>
    public static void Consume(Action<string, int> execute) {
        // The focused input sets this again during the draw phase for as long as it stays focused.
        bool skip = Disabled || CaptureActive || ImGui.GetIO().WantTextInput;

        CaptureActive = false;

        if (skip) {
            Registrations.Clear();

            return;
        }

        int modifiers = GetModifiers();

        foreach (Registration registration in Registrations) {
            int key = registration.Hotkey & ~ModifierMask;
            if (key is < 0 or >= 256) continue;

            // The packed value must match exactly: a missing or extra modifier invalidates the binding.
            bool activated = (KeyStates[key].Flags & KeyFlags.KeyDown) != 0
                && (key | modifiers) == registration.Hotkey;

            if (!activated) continue;

            execute(registration.BarId, registration.SlotIndex);

            if (!registration.Passthrough) BlockGameKey(key);
        }

        Registrations.Clear();
    }

    /// <summary>
    /// Reads one key press for the read-only hotkey input control.
    /// </summary>
    /// <remarks>
    /// Only the KeyUp edge is used, modifier-only and mouse keys are excluded, and escape is handled
    /// separately as "clear the binding".
    /// </remarks>
    public static int? PollCapture() {
        int modifiers = GetModifiers();

        for (int key = 0; key < KeyStates.Length; key++) {
            if (key == EscapeKey) continue;
            if (key is >= 0x10 and <= 0x12 or >= 0xA0 and <= 0xA5) continue;
            if (key <= 2 && modifiers == 0) continue;
            if ((KeyStates[key].Flags & KeyFlags.KeyUp) == 0) continue;

            return key | modifiers;
        }

        return null;
    }

    /// <summary>
    /// Returns true on the frame escape was released.
    /// </summary>
    public static bool IsEscapePressed()
        => (KeyStates[EscapeKey].Flags & KeyFlags.KeyUp) != 0;

    /// <summary>
    /// Packs a key and its modifier into the single integer used by the edge matching.
    /// </summary>
    public static int Pack(VirtualKey key, KeyModifierFlag modifier) {
        if (key == VirtualKey.NO_KEY) return 0;

        int packed = (int) key;

        if (modifier.HasFlag(KeyModifierFlag.Shift)) packed |= ShiftBit;
        if (modifier.HasFlag(KeyModifierFlag.Ctrl))  packed |= CtrlBit;
        if (modifier.HasFlag(KeyModifierFlag.Alt))   packed |= AltBit;

        return packed;
    }

    /// <summary>
    /// Unpacks a stored hotkey back into its key and modifier.
    /// </summary>
    public static (VirtualKey Key, KeyModifierFlag Modifier) Unpack(int packed) {
        if (packed == 0) return (VirtualKey.NO_KEY, KeyModifierFlag.None);

        VirtualKey        key      = (VirtualKey) (packed & ~ModifierMask);
        KeyModifierFlag   modifier = KeyModifierFlag.None;

        if ((packed & CtrlBit) != 0)  modifier |= KeyModifierFlag.Ctrl;
        if ((packed & AltBit) != 0)   modifier |= KeyModifierFlag.Alt;
        if ((packed & ShiftBit) != 0) modifier |= KeyModifierFlag.Shift;

        return (key, modifier);
    }

    /// <summary>
    /// Returns a human readable representation of a packed hotkey.
    /// </summary>
    public static string Describe(int packed) {
        if (packed == 0) return string.Empty;

        (VirtualKey key, KeyModifierFlag modifier) = Unpack(packed);

        string prefix = "";

        if (modifier.HasFlag(KeyModifierFlag.Ctrl))  prefix += "Ctrl + ";
        if (modifier.HasFlag(KeyModifierFlag.Alt))   prefix += "Alt + ";
        if (modifier.HasFlag(KeyModifierFlag.Shift)) prefix += "Shift + ";

        return prefix + key;
    }

    private static int GetModifiers() {
        int modifiers = 0;

        if (IsHeld(0x10)) modifiers |= ShiftBit;
        if (IsHeld(0x11)) modifiers |= CtrlBit;
        if (IsHeld(0x12)) modifiers |= AltBit;

        return modifiers;
    }

    private static bool IsHeld(int key)
        => (KeyStates[key].Flags & KeyFlags.Held) != 0;

    private static void BlockGameKey(int key) {
        try {
            Framework.Service<IKeyState>()[key] = false;
        }
        catch {
            // The key state can be unavailable during a reload; never let that break a hotkey.
        }
    }
}
