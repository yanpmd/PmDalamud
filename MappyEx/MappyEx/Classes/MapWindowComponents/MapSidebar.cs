using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Mappy.Data;

namespace Mappy.Classes.MapWindowComponents;

/// <summary>
/// Tree of every map, drawn on the right hand side of the map window.
/// Two levels are enough to find anything: region, then the map itself. Picking a map shows it and the
/// sidebar stays open, so several maps can be compared without touching the toolbar again.
/// </summary>
public class MapSidebar
{
    private static float RowHeight => ImGui.GetTextLineHeight() + 4.0f * ImGuiHelpers.GlobalScale;

    public unsafe void Draw()
    {
        var currentMapId = AgentMap.Instance()->SelectedMapId;

        ImGui.TextDisabled(Localization.Get("MapSidebar.Title", "Map Browser"));
        ImGui.Separator();

        using var tree = ImRaii.Child("map_sidebar_tree", ImGui.GetContentRegionAvail(), false);
        if (!tree) return;

        foreach (var region in MapIndex.Regions) {
            // A region without a single map that passes the filter is not worth a tree node.
            if (!HasVisibleMap(region)) continue;

            using var regionId = ImRaii.PushId(region.Name);

            if (!ImGui.TreeNodeEx(region.Name, ImGuiTreeNodeFlags.SpanAvailWidth)) continue;

            foreach (var zone in region.Zones) {
                foreach (var area in zone.Areas) {
                    if (!IsVisible(area)) continue;

                    DrawMap(area, currentMapId);
                }
            }

            ImGui.TreePop();
        }
    }

    /// <summary>
    /// Whether the map passes the filter of the map browser.
    /// </summary>
    private static bool IsVisible(MapIndex.Area area) => System.SystemConfig.MapBrowserFilter switch
    {
        MapBrowserFilter.OnlyAetheryte => area.HasAetheryte,
        MapBrowserFilter.HideInstances => !area.IsInstance,
        _ => true,
    };

    private static bool HasVisibleMap(MapIndex.Region region)
    {
        foreach (var zone in region.Zones) {
            foreach (var area in zone.Areas) {
                if (IsVisible(area)) return true;
            }
        }

        return false;
    }

    private static void DrawMap(MapIndex.Area area, uint currentMapId)
    {
        using var mapId = ImRaii.PushId((int)area.Map.RowId);

        if (!ImGui.Selectable(area.Label, area.Map.RowId == currentMapId, ImGuiSelectableFlags.None, new Vector2(0.0f, RowHeight))) return;

        MapNavigator.Show(area.Map.RowId, null);
    }
}
