using Umbra.Windows.Components;

namespace Umbra.CustomHotbar;

/// <summary>
/// A read-only input that captures a key press while it is focused, following QoLBarEx's
/// <c>InputHotkey</c>. Escape clears the binding.
/// </summary>
/// <remarks>
/// There is no global "capture mode" and no rebuild on completion: the control simply reads the
/// keyboard edges while focused and writes the new value straight into the configuration.
/// </remarks>
internal sealed class HotkeyInputNode : ImGuiInputNode {
    public event Action<int>?  OnValueChanged;
    public event Action<bool>? OnPassthroughChanged;

    /// <summary>
    /// Tooltip shown on the small checkbox next to the input.
    /// </summary>
    public string PassthroughTooltip { get; set; } = string.Empty;

    private int    _value;
    private bool   _passthrough;
    private string _display;

    public int Value {
        get => _value;
        set {
            _value   = value;
            _display = CustomHotbarKeybind.Describe(value);
        }
    }

    public bool Passthrough {
        get => _passthrough;
        set => _passthrough = value;
    }

    public HotkeyInputNode(
        string  id,
        int     value,
        bool    passthrough,
        string? label       = null,
        string? description = null
    ) {
        Id          = id;
        Label       = label;
        Description = description;
        LeftMargin  = 0;

        _value       = value;
        _passthrough = passthrough;
        _display     = CustomHotbarKeybind.Describe(value);

        ToggleClass("hotkey-input", true);
    }

    protected override void DrawImGuiInput(Rect bounds) {
        string text = _display;

        ImGui.SetNextItemWidth(Math.Max(60.0f, bounds.Width - 86.0f));

        ImGui.InputText(
            $"##{Id}",
            ref text,
            64,
            ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AllowTabInput
        );

        // Only a focused input captures keys. There is no persistent capture mode: as soon as focus is
        // lost, the next consume pass is free to handle hotkeys again.
        if (ImGui.IsItemActive()) {
            CustomHotbarKeybind.CaptureActive = true;

            int? captured = CustomHotbarKeybind.PollCapture();

            if (captured is not null) SetValue(captured.Value);
        }
        else if (ImGui.IsItemDeactivated() && CustomHotbarKeybind.IsEscapePressed()) {
            // Escape is what ImGui uses to release a text input, so clear on deactivation.
            SetValue(0);
        }

        ImGui.SameLine();

        bool pass = _passthrough;

        if (ImGui.Checkbox($"##{Id}-pass", ref pass)) {
            _passthrough = pass;

            OnPassthroughChanged?.Invoke(pass);
        }

        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(PassthroughTooltip)) {
            ImGui.SetTooltip(PassthroughTooltip);
        }
    }

    private void SetValue(int value) {
        if (_value == value) return;

        Value = value;

        OnValueChanged?.Invoke(value);
    }
}
