using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Umbra.Common;

namespace Umbra.QoLBar;

/// <summary>
/// Global hotkey polling and capture. Ported from QoLBarCN's Keybind class.
/// </summary>
public static class KeybindManager
{
    // TODO: Stolen from Winforms, swap to Dalamud's
    public enum Keys
    {
        KeyCode = 0xFFFF,
        Modifiers = -65536,
        None = 0x0,
        LButton = 0x1,
        RButton = 0x2,
        Cancel = 0x3,
        MButton = 0x4,
        XButton1 = 0x5,
        XButton2 = 0x6,
        Back = 0x8,
        Tab = 0x9,
        LineFeed = 0xA,
        Clear = 0xC,
        Return = 0xD,
        Enter = 0xD,
        ShiftKey = 0x10,
        ControlKey = 0x11,
        Menu = 0x12,
        Pause = 0x13,
        Capital = 0x14,
        CapsLock = 0x14,
        KanaMode = 0x15,
        HanguelMode = 0x15,
        HangulMode = 0x15,
        JunjaMode = 0x17,
        FinalMode = 0x18,
        HanjaMode = 0x19,
        KanjiMode = 0x19,
        KanjiMode2 = 0x19,
        Escape = 0x1B,
        IMEConvert = 0x1C,
        IMENonconvert = 0x1D,
        IMEAccept = 0x1E,
        IMEAceept = 0x1E,
        IMEModeChange = 0x1F,
        Space = 0x20,
        Prior = 0x21,
        PageUp = 0x21,
        Next = 0x22,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Select = 0x29,
        Print = 0x2A,
        Execute = 0x2B,
        Snapshot = 0x2C,
        PrintScreen = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,
        Help = 0x2F,
        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,
        LWin = 0x5B,
        RWin = 0x5C,
        Apps = 0x5D,
        Sleep = 0x5F,
        NumPad0 = 0x60,
        NumPad1 = 0x61,
        NumPad2 = 0x62,
        NumPad3 = 0x63,
        NumPad4 = 0x64,
        NumPad5 = 0x65,
        NumPad6 = 0x66,
        NumPad7 = 0x67,
        NumPad8 = 0x68,
        NumPad9 = 0x69,
        Multiply = 0x6A,
        Add = 0x6B,
        Separator = 0x6C,
        Subtract = 0x6D,
        Decimal = 0x6E,
        Divide = 0x6F,
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
        F13 = 0x7C,
        F14 = 0x7D,
        F15 = 0x7E,
        F16 = 0x7F,
        F17 = 0x80,
        F18 = 0x81,
        F19 = 0x82,
        F20 = 0x83,
        F21 = 0x84,
        F22 = 0x85,
        F23 = 0x86,
        F24 = 0x87,
        NumLock = 0x90,
        Scroll = 0x91,
        LShiftKey = 0xA0,
        RShiftKey = 0xA1,
        LControlKey = 0xA2,
        RControlKey = 0xA3,
        LMenu = 0xA4,
        RMenu = 0xA5,
        BrowserBack = 0xA6,
        BrowserForward = 0xA7,
        BrowserRefresh = 0xA8,
        BrowserStop = 0xA9,
        BrowserSearch = 0xAA,
        BrowserFavorites = 0xAB,
        BrowserHome = 0xAC,
        VolumeMute = 0xAD,
        VolumeDown = 0xAE,
        VolumeUp = 0xAF,
        MediaNextTrack = 0xB0,
        MediaPreviousTrack = 0xB1,
        MediaStop = 0xB2,
        MediaPlayPause = 0xB3,
        LaunchMail = 0xB4,
        SelectMedia = 0xB5,
        LaunchApplication1 = 0xB6,
        LaunchApplication2 = 0xB7,
        OemSemicolon = 0xBA,
        Oem1 = 0xBA,
        Oemplus = 0xBB,
        Oemcomma = 0xBC,
        OemMinus = 0xBD,
        OemPeriod = 0xBE,
        OemQuestion = 0xBF,
        Oem2 = 0xBF,
        Oemtilde = 0xC0,
        Oem3 = 0xC0,
        OemOpenBrackets = 0xDB,
        Oem4 = 0xDB,
        OemPipe = 0xDC,
        Oem5 = 0xDC,
        OemCloseBrackets = 0xDD,
        Oem6 = 0xDD,
        OemQuotes = 0xDE,
        Oem7 = 0xDE,
        Oem8 = 0xDF,
        OemBackslash = 0xE2,
        Oem102 = 0xE2,
        ProcessKey = 0xE5,
        Packet = 0xE7,
        Attn = 0xF6,
        Crsel = 0xF7,
        Exsel = 0xF8,
        EraseEof = 0xF9,
        Play = 0xFA,
        Zoom = 0xFB,
        NoName = 0xFC,
        Pa1 = 0xFD,
        OemClear = 0xFE,
        Shift = 0x10000,
        Control = 0x20000,
        Alt = 0x40000
    }

    private const int modifierMask = -1 << (int)Keys.ShiftKey;
    private const int shiftModifier = 1 << (int)Keys.ShiftKey;
    private const int controlModifier = 1 << (int)Keys.ControlKey;
    private const int altModifier = 1 << (int)Keys.Menu;

    public struct QoLKeyState
    {
        [Flags]
        public enum State
        {
            None = 0,
            Held = 1,
            KeyDown = 2,
            KeyUp = 4,
            ShortHold = 8
        }

        public State CurrentState { get; private set; }
        public float HoldDuration { get; private set; }
        public bool wasShortHeld;

        public void Update(bool down)
        {
            if (down)
            {
                var lastState = CurrentState;
                CurrentState = State.Held;
                if ((lastState & State.Held) == 0)
                    CurrentState |= State.KeyDown;
                else if (HoldDuration >= 0.2f)
                    CurrentState |= State.ShortHold;

                HoldDuration += DeltaTime;
            }
            else if (CurrentState != State.None)
            {
                wasShortHeld = (CurrentState & State.ShortHold) != 0;
                CurrentState = CurrentState != State.KeyUp ? State.KeyUp : State.None;
                HoldDuration = 0;
            }
        }
    }

    private static readonly Stopwatch deltaStopwatch = Stopwatch.StartNew();
    private static float deltaTime = 0;

    /// <summary>Time in seconds since the last call to <see cref="UpdateKeyStates"/>.</summary>
    public static float DeltaTime => deltaTime;

    private static readonly byte[] keyboardState = new byte[256];
    private static readonly QoLKeyState[] keyStates = new QoLKeyState[keyboardState.Length];
    private static readonly HashSet<int> conflictingHotkeys = new();
    private static bool Disabled => GameActions.IsGameTextInputActive || !GameActions.IsGameFocused || ImGui.GetIO().WantCaptureKeyboard;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    /// <summary>
    /// Polls the keyboard and updates the internal key states. Should be
    /// invoked once per framework tick.
    /// </summary>
    public static void UpdateKeyStates()
    {
        deltaTime = (float)deltaStopwatch.Elapsed.TotalSeconds;
        deltaStopwatch.Restart();

        GetKeyStates();
    }

    private static void GetKeyStates()
    {
        GetKeyboardState(keyboardState);
        for (int i = 0; i < keyStates.Length; i++)
            keyStates[i].Update((keyboardState[i] & 0x80) != 0);
    }

    public static bool CheckKeyState(int i, QoLKeyState.State state) => i is >= 0 and < 256 && (keyStates[i].CurrentState & state) != 0;

    public static bool IsHotkeyActivated(int i, bool onUp) => i is >= 0 and < 256 && (onUp
        ? CheckKeyState(i, QoLKeyState.State.KeyUp)
        : CheckKeyState(i, QoLKeyState.State.KeyDown));

    /// <summary>
    /// Returns true during the frame the hotkey's key combination is pressed down.
    /// </summary>
    public static bool IsHotkeyPressed(int hotkey)
    {
        if (Disabled) return false;

        var key = GetBaseHotkey(hotkey);
        if (key <= 0 || key >= 256) return false;

        return CheckKeyState(key, QoLKeyState.State.KeyDown) && hotkey == (key | GetModifiers());
    }

    public static int GetModifiers()
    {
        var modifiers = 0;
        var io = ImGui.GetIO();
        if (io.KeyShift)
            modifiers |= shiftModifier;
        if (io.KeyCtrl)
            modifiers |= controlModifier;
        if (io.KeyAlt)
            modifiers |= altModifier;
        return modifiers;
    }

    private static int GetBaseHotkey(int hotkey) => hotkey & ~modifierMask;

    public static bool IsHotkeyHeld(int hotkey, bool blockGame)
    {
        if (Disabled) return false;

        var key = GetBaseHotkey(hotkey);
        var isDown = CheckKeyState(key, QoLKeyState.State.Held) && hotkey == (key | GetModifiers());

        if (isDown)
        {
            if (conflictingHotkeys.Contains(hotkey))
                isDown = CheckKeyState(key, QoLKeyState.State.ShortHold);

            if (blockGame)
                BlockGameKey(key);
        }

        return isDown;
    }

    public static void BlockGameKey(int key)
    {
        try { Framework.Service<IKeyState>()[key] = false; }
        catch { }
    }

    /// <summary>
    /// Registers all hotkeys of the given shortcuts and executes them when
    /// they are activated. Conflicting hotkeys are resolved by requiring the
    /// key to be released before triggering again.
    /// </summary>
    public static void DoHotkeys(IEnumerable<(BarCfg bar, ShCfg shortcut)> hotkeys)
    {
        conflictingHotkeys.Clear();

        if (Disabled)
        {
            return;
        }

        // Detect conflicting hotkeys first.
        foreach (var (_, sh) in hotkeys)
        {
            if (sh.Hotkey > 0 && hotkeys.Any(kv => kv.shortcut.Hotkey == sh.Hotkey && !ReferenceEquals(kv.shortcut, sh)))
                conflictingHotkeys.Add(sh.Hotkey);
        }

        var modifiers = GetModifiers();
        foreach (var (bar, sh) in hotkeys)
        {
            var config = sh;
            var key = GetBaseHotkey(config.Hotkey);
            if (key <= 0 || key >= 256) continue;

            var state = keyStates[key];
            var onUp = conflictingHotkeys.Contains(config.Hotkey);
            var activated = IsHotkeyActivated(key, onUp) && (!onUp || !state.wasShortHeld) && (key | modifiers) == config.Hotkey;
            if (!activated) continue;

            QoLBarManager.Instance.ExecuteShortcut(bar, sh);

            if (!config.KeyPassthrough)
                BlockGameKey(key);
        }
    }

    public static bool InputHotkey(string id, ref int hotkey)
    {
        var dispKey = GetKeyName(hotkey);
        ImGui.InputText($"{id}##{hotkey}", ref dispKey, 200, ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AllowTabInput); // delete the box to delete focus 4head
        if (ImGui.IsItemActive())
        {
            var modifiers = GetModifiers();
            for (var k = 0; k < keyStates.Length; k++)
            {
                var keyState = keyStates[k];
                if (k is >= 16 and <= 18 or >= 160 and <= 165 || k <= 2 && modifiers == 0 || (keyState.CurrentState & QoLKeyState.State.KeyUp) == 0) continue;
                hotkey = k | modifiers;
                return true;
            }
        }

        if (!ImGui.IsItemDeactivated() || !ImGui.GetIO().KeysDown[(int)Keys.Escape]) return false;

        hotkey = 0;
        return true;
    }

    private static readonly Dictionary<Keys, string> _keynames = new()
    {
        [Keys.LButton] = "Mouse 1",
        [Keys.RButton] = "Mouse 2",
        [Keys.MButton] = "Mouse 3",
        [Keys.XButton1] = "Mouse 4",
        [Keys.XButton2] = "Mouse 5",
        [Keys.ShiftKey] = "Shift",
        [Keys.ControlKey] = "Ctrl",
        [Keys.Menu] = "Alt",
        [Keys.PageUp] = "PageUp",
        [Keys.PageDown] = "PageDown",
        [Keys.PrintScreen] = "PrintScreen",
        [Keys.D0] = "0",
        [Keys.D1] = "1",
        [Keys.D2] = "2",
        [Keys.D3] = "3",
        [Keys.D4] = "4",
        [Keys.D5] = "5",
        [Keys.D6] = "6",
        [Keys.D7] = "7",
        [Keys.D8] = "8",
        [Keys.D9] = "9",
        [Keys.Scroll] = "ScrollLock",
        [Keys.OemSemicolon] = ";",
        [Keys.Oemplus] = "=",
        [Keys.OemMinus] = "-",
        [Keys.Oemcomma] = ",",
        [Keys.OemPeriod] = ".",
        [Keys.OemQuestion] = "/",
        [Keys.Oemtilde] = "`",
        [Keys.OemOpenBrackets] = "[",
        [Keys.OemPipe] = "\\",
        [Keys.OemCloseBrackets] = "]",
        [Keys.OemQuotes] = "'"
    };

    public static string GetKeyName(int k)
    {
        var key = (Keys)k;
        string mod = string.Empty;
        if ((key & Keys.Shift) != 0)
        {
            mod += "Shift + ";
            key -= Keys.Shift;
        }
        if ((key & Keys.Control) != 0)
        {
            mod += "Ctrl + ";
            key -= Keys.Control;
        }
        if ((key & Keys.Alt) != 0)
        {
            mod += "Alt + ";
            key -= Keys.Alt;
        }
        if (_keynames.TryGetValue(key, out var name))
            return mod + name;
        else
            return mod + key;
    }
}
