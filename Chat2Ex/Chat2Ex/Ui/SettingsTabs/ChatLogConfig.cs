using System.Numerics;
using Chat2Ex.Resources;
using Chat2Ex.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Chat2Ex.Ui.SettingsTabs;

/// <summary>
///     Chat window settings. The original ImGui-only options (ImGui title bars, Dalamud styles,
///     sidebar tab view, ...) have no counterpart in the native chat window and were removed instead
///     of being left as switches that do nothing.
/// </summary>
public sealed class ChatLogConfig : ISettingsTab
{
    private readonly Plugin Plugin;
    private Configuration Mutable { get; }

    public string Name => Language.Options_ChatLog_Tab + "###tabs-chatlog";

    public ChatLogConfig(Plugin plugin, Configuration mutable)
    {
        Plugin = plugin;
        Mutable = mutable;
    }

    public void Draw(bool changed)
    {
        using (ImRaii.TextWrapPos(0.0f))
        {
            ImGuiUtil.OptionCheckbox(ref Mutable.PlaySounds, Language.Options_PlaySounds_Name, Language.Options_PlaySounds_Description);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.NativeItemTooltips, Language.Options_NativeItemTooltips_Name, string.Format(Language.Options_NativeItemTooltips_Description, Plugin.PluginName));
            ImGui.Spacing();

            ImGuiUtil.DragFloatVertical(Language.Options_WindowOpacity_Name, ref Mutable.WindowAlpha, .25f, 0f, 100f, $"{Mutable.WindowAlpha:N2}%%", ImGuiSliderFlags.AlwaysClamp);
            ImGui.Spacing();

            if (ImGuiUtil.InputIntVertical(Language.Options_MaxLinesToShow_Name, Language.Options_MaxLinesToShow_Description, ref Mutable.MaxLinesToRender))
                Mutable.MaxLinesToRender = Math.Clamp(Mutable.MaxLinesToRender, 1, 10_000);
            ImGui.Spacing();

            ImGui.Separator();
            ImGui.TextUnformatted("原生聊天窗口 / Native chat window");
            ImGui.Spacing();

            if (ImGuiUtil.InputIntVertical("字体大小 / Font size", "10 - 24", ref Mutable.ChatFontSize))
                Mutable.ChatFontSize = Math.Clamp(Mutable.ChatFontSize, 10, 24);
            ImGui.Spacing();

            ImGuiUtil.DragFloatVertical("背景不透明度 / Background opacity", ref Mutable.ChatBackgroundOpacity, 1f, 0f, 100f, $"{Mutable.ChatBackgroundOpacity:N0}%%", ImGuiSliderFlags.AlwaysClamp);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.LockWindowPosition, "锁定窗口位置 / Lock window position", "开启后聊天窗口不能被拖动。");
            ImGui.Spacing();

            var size = Mutable.ChatWindowSize;
            ImGui.TextUnformatted("窗口大小 / Window size");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat2("##chat2ex-window-size", ref size, 1f, 320f, 1920f, "%.0f"))
                Mutable.ChatWindowSize = size;
            ImGui.Spacing();

            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted(Language.Options_ChatTabForwardKeybind_Name);
            ImGui.SetNextItemWidth(-1);
            ImGuiUtil.KeybindInput("ChatTabForwardKeybind", ref Mutable.ChatTabForward);

            ImGui.TextUnformatted(Language.Options_ChatTabBackwardKeybind_Name);
            ImGui.SetNextItemWidth(-1);
            ImGuiUtil.KeybindInput("ChatTabBackwardKeybind", ref Mutable.ChatTabBackward);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted(Language.Options_AdjustPosition_Name);
            ImGui.SetNextItemWidth(-1);
            var pos = Plugin.ChatUi.MainWindowPosition.GetValueOrDefault();
            if (ImGui.DragFloat2($"##{Language.Options_AdjustPosition_Name}", ref pos, 1, 0, float.MaxValue, "%.0fpx"))
                Plugin.ChatUi.MoveMainWindow(pos);
            ImGuiUtil.WarningText(Language.Options_AdjustPosition_Warning);
            ImGui.Spacing();
        }
    }
}
