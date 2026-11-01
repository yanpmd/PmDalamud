using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

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
            using var regionId = ImRaii.PushId(region.Name);

            if (!ImGui.TreeNodeEx(region.Name, ImGuiTreeNodeFlags.SpanAvailWidth)) continue;

            foreach (var zone in region.Zones) {
                foreach (var area in zone.Areas) {
                    DrawMap(area, currentMapId);
                }
            }

            ImGui.TreePop();
        }
    }

    private static void DrawMap(MapIndex.Area area, uint currentMapId)
    {
        using var mapId = ImRaii.PushId((int)area.Map.RowId);

        if (!ImGui.Selectable(area.Label, area.Map.RowId == currentMapId, ImGuiSelectableFlags.None, new Vector2(0.0f, RowHeight))) return;

        MapNavigator.Show(area.Map.RowId, null);
    }
}
