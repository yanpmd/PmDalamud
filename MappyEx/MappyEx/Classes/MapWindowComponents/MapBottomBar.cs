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
/// Bar along the bottom of the map: the character / cursor coordinates and the map browser toggle.
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
        var buttonWidth = SidebarButtonWidth();

        ImGui.SetCursorPos(new Vector2(2.5f * scale, 2.0f * scale));

        // The browser toggle sits at the far right end of the bar.
        ImGui.SetCursorPosX(rightEdge - buttonWidth);
        DrawSidebarButton();

        if (System.SystemConfig.ShowCoordinateBar) {
            var characterEnd = DrawCharacterCoordinates();
            DrawCursorCoordinates(isMapHovered, mapDrawOffset, characterEnd, rightEdge, buttonWidth);
        }

        return ImGui.IsMouseHoveringRect(barMin, barMin + barSize);
    }

    private static void DrawSidebarButton()
    {
        var isOpen = System.SystemConfig.ShowMapSidebar;

        using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetStyle().GetColor(ImGuiCol.ButtonActive), isOpen)) {
            if (MappyGuiTweaks.IconButton(FontAwesomeIcon.FolderTree, "mapSidebar", Localization.Get("Toolbar.ToggleMapSidebar", "Map Browser"))) {
                System.MapWindow.ToggleSidebar();
            }
        }
    }

    /// <summary>
    /// Width of the browser button, the icon font has to be active to measure the glyph.
    /// </summary>
    private static float SidebarButtonWidth()
    {
        using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push()) {
            return ImGui.CalcTextSize(FontAwesomeIcon.FolderTree.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2.0f;
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
    /// Cursor coordinates right before the browser button, skipped when there is no room for them.
    /// </summary>
    private static void DrawCursorCoordinates(bool isMapHovered, Vector2 mapDrawOffset, float characterEnd, float rightEdge, float buttonWidth)
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
        var x = rightEdge - buttonWidth - gap - textWidth;

        // No room between the character coordinates and the button.
        if (x < characterEnd + gap) return;

        ImGui.SameLine();
        ImGui.SetCursorPosX(x);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(System.SystemConfig.CoordinateTextColor, cursorText);
    }
}
