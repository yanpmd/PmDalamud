using System.Numerics;
using Chat2Ex.Resources;
using Chat2Ex.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Chat2Ex.Ui.SettingsTabs;

/// <summary>
///     Chat log behaviour settings. Native window appearance/geometry lives in
///     <see cref="ChatWindowAppearance"/>; ImGui-only options that have no native counterpart were
///     removed instead of being left as switches that do nothing.
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

            if (ImGuiUtil.InputIntVertical(Language.Options_MaxLinesToShow_Name, Language.Options_MaxLinesToShow_Description, ref Mutable.MaxLinesToRender))
                Mutable.MaxLinesToRender = Math.Clamp(Mutable.MaxLinesToRender, 1, 10_000);
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
        }
    }
}
