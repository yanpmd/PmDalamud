using System.Numerics;
using Chat2Ex.Resources;
using Chat2Ex.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Chat2Ex.Ui.SettingsTabs;

/// <summary>
///     Appearance and geometry of the native chat window. These were previously mixed into the chat
///     log settings page; they all live here so window customisation is in one place.
/// </summary>
public sealed class ChatWindowAppearance : ISettingsTab
{
    private readonly Plugin Plugin;
    private Configuration Mutable { get; }

    public string Name => "窗口外观 / Window###tabs-window-appearance";

    public ChatWindowAppearance(Plugin plugin, Configuration mutable)
    {
        Plugin = plugin;
        Mutable = mutable;
    }

    public void Draw(bool changed)
    {
        using (ImRaii.TextWrapPos(0.0f))
        {
            if (ImGuiUtil.InputIntVertical("字体大小 / Font size", "10 - 24", ref Mutable.ChatFontSize))
                Mutable.ChatFontSize = Math.Clamp(Mutable.ChatFontSize, 10, 24);
            ImGui.Spacing();

            var background = ColourUtil.RgbaToVector4(Mutable.ChatBackgroundColor) ?? new Vector4(0, 0, 0, 0.65f);
            ImGui.TextUnformatted("背景颜色 / Background colour");
            if (ImGui.ColorEdit4("##chat2ex-background-colour", ref background,
                    ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                Mutable.ChatBackgroundColor = ColourUtil.Vector4ToRgba(background);
            ImGuiUtil.HelpText("RGB 为染色，Alpha 为透明度。0% 全透明，100% 不透明。");
            ImGui.Spacing();

            ImGuiUtil.DragFloatVertical(Language.Options_WindowOpacity_Name, ref Mutable.WindowAlpha, .25f, 0f, 100f, $"{Mutable.WindowAlpha:N2}%%", ImGuiSliderFlags.AlwaysClamp);
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
