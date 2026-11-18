using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Extensions;
using Mappy.Data;

namespace Mappy.Classes.MapWindowComponents;

public enum MapSidebarMode
{
    Browser,
    Favorites,
}

/// <summary>
/// Right hand sidebar of the map window. The map browser and the favorites are two pages of this one sidebar:
/// they share the exact same spot and switching pages keeps the sidebar open.
/// </summary>
public class MapSidebarPanel
{
    private readonly MapSidebar browser = new();
    private readonly FavoriteSidebar favorites = new();

    public MapSidebarMode Mode { get; private set; } = MapSidebarMode.Browser;

    public void ToggleBrowser() => Toggle(MapSidebarMode.Browser);

    public void ToggleFavorites() => Toggle(MapSidebarMode.Favorites);

    private void Toggle(MapSidebarMode mode)
    {
        if (System.SystemConfig.ShowMapSidebar && Mode == mode) {
            Close();

            return;
        }

        System.SystemConfig.ShowMapSidebar = true;
        Mode = mode;

        if (mode is MapSidebarMode.Favorites) favorites.Open();
        else favorites.Close();

        SystemConfig.Save();
    }

    public void Close()
    {
        if (!System.SystemConfig.ShowMapSidebar) return;

        System.SystemConfig.ShowMapSidebar = false;
        favorites.Close();
        SystemConfig.Save();
    }

    public void Draw(float width, float height)
    {
        using var sidebar = ImRaii.Child("map_sidebar", new Vector2(width, height), true);
        if (!sidebar) return;

        DrawTabs();

        if (Mode is MapSidebarMode.Browser) {
            browser.Draw();
        }
        else {
            favorites.DrawContents();
        }
    }

    private void DrawTabs()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var browserLabel = Localization.Get("MapSidebar.Tab.Browser", "Map Browser");
        var favoriteLabel = Localization.Get("MapSidebar.Tab.Favorites", "Favorites");
        var tabWidth = MathF.Max(ImGui.CalcTextSize(browserLabel).X, ImGui.CalcTextSize(favoriteLabel).X) + 10.0f * scale;

        DrawTab(browserLabel, "browserTab", Mode is MapSidebarMode.Browser, tabWidth, ToggleBrowser);

        ImGui.SameLine();

        DrawTab(favoriteLabel, "favoriteTab", Mode is MapSidebarMode.Favorites, tabWidth, ToggleFavorites);

        var closeWidth = ImGui.CalcTextSize("x").X + ImGui.GetStyle().FramePadding.X * 2.0f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - closeWidth);
        if (ImGui.Button("x")) Close();

        ImGui.Separator();
    }

    private static void DrawTab(string label, string id, bool active, float width, Action onClick)
    {
        using var pushedId = ImRaii.PushId(id);
        using var color = ImRaii.PushColor(ImGuiCol.Button, ImGui.GetStyle().GetColor(ImGuiCol.ButtonActive), active);

        if (ImGui.Button(label, new Vector2(width, 0.0f))) onClick();
    }
}
