using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Extensions;

namespace Mappy.Classes.MapWindowComponents;

/// <summary>
/// Bar along the bottom of the map: the character / cursor coordinates and the two sidebar page toggles.
/// Shares its visibility with the top toolbar, both fit on one line without covering the map twice.
/// </summary>
public unsafe class MapBottomBar
{
    private static float BarHeight => ImGui.GetFrameHeight() + 4.0f * ImGuiHelpers.GlobalScale;

    /// <summary>
    /// Draws the bar and reports if the mouse is over it,
    /// so clicks on it are not treated as clicks on the map.
    /// </summary>
    public bool Draw(bool isMapHovered, Vector2 mapDrawOffset)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var barSize = new Vector2(ImGui.GetContentRegionMax().X, BarHeight);

        ImGui.SetCursorPos(new Vector2(0.0f, ImGui.GetContentRegionMax().Y - barSize.Y));

        // The bar is drawn on top of the map, its own area must not react to map clicks.
        var barMin = ImGui.GetCursorScreenPos();

        using var background = ImRaii.PushColor(ImGuiCol.ChildBg, Vector4.Zero with { W = System.SystemConfig.CoordinateBarFade });
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        using var bar = ImRaii.Child("map_bottom_bar", barSize);
        if (!bar) return false;

        var rightEdge = ImGui.GetContentRegionMax().X - 2.5f * scale;
        var buttonsWidth = SidebarButtonsWidth();

        ImGui.SetCursorPos(new Vector2(2.5f * scale, 2.0f * scale));

        // The map browser and favorites toggles sit together at the far right end of the bar.
        ImGui.SetCursorPosX(rightEdge - buttonsWidth);
        DrawSidebarButtons();

        if (System.SystemConfig.ShowCoordinateBar) {
            var characterEnd = DrawCharacterCoordinates();
            DrawCursorCoordinates(isMapHovered, mapDrawOffset, characterEnd, rightEdge, buttonsWidth);
        }

        return ImGui.IsMouseHoveringRect(barMin, barMin + barSize);
    }

    private static void DrawSidebarButtons()
    {
        var sidebarOpen = System.SystemConfig.ShowMapSidebar;

        using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetStyle().GetColor(ImGuiCol.ButtonActive), sidebarOpen && !System.MapWindow.IsFavoriteSidebarOpen)) {
            if (MappyGuiTweaks.IconButton(FontAwesomeIcon.FolderTree, "mapSidebar", Localization.Get("Toolbar.ToggleMapSidebar", "Map Browser"))) {
                System.MapWindow.ToggleSidebar();
            }
        }

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetStyle().GetColor(ImGuiCol.ButtonActive), System.MapWindow.IsFavoriteSidebarOpen)) {
            if (MappyGuiTweaks.IconButton(FontAwesomeIcon.Star, "favoriteSidebar", Localization.Get("Toolbar.ToggleFavorites", "Favorites"))) {
                System.MapWindow.ToggleFavoriteSidebar();
            }
        }
    }

    /// <summary>
    /// Width of both sidebar buttons including their spacing, the icon font has to be active to measure the glyphs.
    /// </summary>
    private static float SidebarButtonsWidth()
    {
        using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push()) {
            var iconWidth = MathF.Max(
                ImGui.CalcTextSize(FontAwesomeIcon.FolderTree.ToIconString()).X,
                ImGui.CalcTextSize(FontAwesomeIcon.Star.ToIconString()).X);

            return iconWidth * 2.0f + ImGui.GetStyle().FramePadding.X * 4.0f + ImGui.GetStyle().ItemSpacing.X;
        }
    }

    /// <summary>
    /// Character coordinates on the left side of the bar, returns where the text ends.
    /// </summary>
    private static float DrawCharacterCoordinates()
    {
        // Only meaningful while the map of the current territory is shown.
        if (AgentMap.Instance()->SelectedMapId != AgentMap.Instance()->CurrentMapId) return 0.0f;

        var offsetX = -AgentMap.Instance()->SelectedOffsetX;
        var offsetY = -AgentMap.Instance()->SelectedOffsetY;
        var scale = AgentMap.Instance()->SelectedMapSizeFactor;

        var characterMapPosition = MapUtil.WorldToMap(Service.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero, offsetX, offsetY, 0, (uint)scale);
        var characterPosition = Localization.F("CoordinateBar.Character", $"{characterMapPosition.X:F1}", $"{characterMapPosition.Y:F1}");

        var leftEdge = 2.5f * ImGuiHelpers.GlobalScale;

        ImGui.SameLine();
        ImGui.SetCursorPosX(leftEdge);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(System.SystemConfig.CoordinateTextColor, characterPosition);

        return leftEdge + ImGui.CalcTextSize(characterPosition).X;
    }

    /// <summary>
    /// Cursor coordinates right before the sidebar buttons, skipped when there is no room for them.
    /// </summary>
    private static void DrawCursorCoordinates(bool isMapHovered, Vector2 mapDrawOffset, float characterEnd, float rightEdge, float buttonsWidth)
    {
        if (!isMapHovered) return;

        var offsetX = -AgentMap.Instance()->SelectedOffsetX;
        var offsetY = -AgentMap.Instance()->SelectedOffsetY;
        var scale = AgentMap.Instance()->SelectedMapSizeFactor;

        var cursorPosition = ImGui.GetMousePos() - mapDrawOffset;
        cursorPosition -= System.MapRenderer.DrawPosition;
        cursorPosition /= MapRenderer.MapRenderer.Scale;
        cursorPosition -= new Vector2(1024.0f, 1024.0f);
        cursorPosition -= new Vector2(offsetX, offsetY);
        cursorPosition /= AgentMap.Instance()->SelectedMapSizeFactorFloat;

        var cursorMapPosition = MapUtil.WorldToMap(new Vector3(cursorPosition.X, 0.0f, cursorPosition.Y), offsetX, offsetY, 0, (uint)scale);
        var cursorText = Localization.F("CoordinateBar.Cursor", $"{cursorMapPosition.X:F1}", $"{cursorMapPosition.Y:F1}");

        var gap = 4.0f * ImGuiHelpers.GlobalScale;
        var textWidth = ImGui.CalcTextSize(cursorText).X;
        var x = rightEdge - buttonsWidth - gap - textWidth;

        // No room between the character coordinates and the buttons.
        if (x < characterEnd + gap) return;

        ImGui.SameLine();
        ImGui.SetCursorPosX(x);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(System.SystemConfig.CoordinateTextColor, cursorText);
    }
}
