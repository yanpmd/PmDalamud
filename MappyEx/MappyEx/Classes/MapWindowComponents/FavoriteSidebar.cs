using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Mappy.Controllers;
using Mappy.Data;

namespace Mappy.Classes.MapWindowComponents;

/// <summary>
/// Favorites page of the shared map overlay. Favorites are listed by group, a left click navigates to the point
/// and a right click opens its settings menu. The panel frame and tabs belong to <see cref="MapOverlayPanel"/>.
/// </summary>
public class FavoriteSidebar
{
    private const string PopupName = "##mappy_favorite_menu";

    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void DrawContents()
    {
        if (!IsOpen) return;

        using var list = ImRaii.Child("mappy_favorite_list", ImGui.GetContentRegionAvail(), false);
        if (!list) return;

        var config = System.FavoriteConfig;
        if (config.Favorites.Count is 0) {
            ImGuiHelpers.ScaledDummy(6.0f);
            ImGui.TextWrapped(Localization.Get("FavoriteSidebar.Empty", "No favorites yet. Use the + button above or right click the map to save your current location."));

            return;
        }

        foreach (var entry in config.GroupedFavorites) {
            using var groupId = ImRaii.PushId(entry.Key.Length is 0 ? "none" : entry.Key);

            var label = $"{config.GetGroupName(entry.Key)}  ({entry.Count()})";
            if (!ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth)) continue;

            foreach (var point in entry) {
                DrawPoint(point);
            }

            ImGui.TreePop();
        }
    }

    private static void DrawPoint(FavoritePoint point)
    {
        using var id = ImRaii.PushId(point.Id);

        var rowHeight = ImGui.GetTextLineHeight() + 4.0f * ImGuiHelpers.GlobalScale;
        var coordinate = point.GetMapCoordinate();
        var label = $"{point.DisplayName}  ({coordinate.X:F1}, {coordinate.Y:F1})";

        if (IconHelper.GetTexture(point.IconId) is { } texture) {
            var iconSize = ImGui.GetTextLineHeight();
            ImGui.Image(texture.Handle, new Vector2(iconSize, iconSize));
            ImGui.SameLine();
        }

        if (ImGui.Selectable(label, false, ImGuiSelectableFlags.None, new Vector2(0.0f, rowHeight))) {
            Navigate(point, fly: false);
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            ImGui.OpenPopup(PopupName);
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip($"{point.GetMapName()}\n{point.Note}".TrimEnd('\n'));
        }

        DrawMenu(point);
    }

    private static void DrawMenu(FavoritePoint point)
    {
        using var popup = ImRaii.Popup(PopupName);
        if (!popup) return;

        if (ImGui.MenuItem(Localization.Get("FavoriteSidebar.GoTo", "Go To"))) Navigate(point, fly: false);
        if (ImGui.MenuItem(Localization.Get("FavoriteSidebar.FlyTo", "Fly To"))) Navigate(point, fly: true);

        ImGui.Separator();

        if (ImGui.MenuItem(Localization.Get("FavoriteSidebar.LocateOnMap", "Locate on Map"))) {
            System.SystemConfig.FollowPlayer = false;
            System.IntegrationsController.OpenMap(point.MapId);
            System.MapRenderer.CenterOnCoordinate(point.Position);
        }

        if (ImGui.MenuItem(Localization.Get("FavoriteSidebar.Edit", "Settings..."))) {
            System.FavoriteEditorWindow.Edit(point);
        }

        if (ImGui.MenuItem(Localization.Get("FavoriteSidebar.Delete", "Delete"))) {
            System.FavoriteConfig.Remove(point);
        }
    }

    private static void Navigate(FavoritePoint point, bool fly)
        => AethernetNavigator.NavigateToMarker(point.WorldPosition, point.MapId, fly);

    public static void AddCurrentLocation()
    {
        var point = FavoritePoint.CaptureCurrentLocation();
        System.FavoriteConfig.Add(point);
        System.FavoriteEditorWindow.Edit(point);
    }
}
