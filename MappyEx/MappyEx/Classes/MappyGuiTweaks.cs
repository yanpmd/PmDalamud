using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Mappy.Classes;

public static class MappyGuiTweaks
{
    public static bool IconButton(FontAwesomeIcon icon, string id, string? tooltip)
    {
        using var imRaiiId = ImRaii.PushId(id);

        bool result;

        using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push()) {
            result = ImGui.Button($"{icon.ToIconString()}");
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && tooltip is not null) {
            ImGui.SetTooltip(tooltip);
        }

        return result;
    }

    /// <summary>
    /// Button drawn with a game icon instead of a font glyph, highlighted while <paramref name="active"/> is true.
    /// </summary>
    public static bool GameIconButton(uint iconId, string id, string? tooltip, bool active)
    {
        using var imRaiiId = ImRaii.PushId(id);

        var texture = Service.TextureProvider.GetFromGameIcon(iconId).GetWrapOrEmpty();
        var size = ImGuiHelpers.ScaledVector2(24.0f, 24.0f);

        var buttonColor = active ? new Vector4(0.1f, 0.7f, 0.2f, 1.0f) : new Vector4(0.28f, 0.28f, 0.28f, 1.0f);

        using var color = ImRaii.PushColor(ImGuiCol.Button, buttonColor);
        var result = ImGui.ImageButton(texture.Handle, size);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && tooltip is not null) {
            ImGui.SetTooltip(tooltip);
        }

        return result;
    }
}
