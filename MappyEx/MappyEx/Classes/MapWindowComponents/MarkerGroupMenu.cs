using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Mappy.Data;

namespace Mappy.Classes.MapWindowComponents;

/// <summary>
/// A single toolbar button that opens a dropdown with every marker group on the selected map.
/// One entry point keeps the toolbar layout stable no matter how many groups exist, and nothing can overlap
/// the settings / close buttons anymore.
/// </summary>
public static class MarkerGroupMenu
{
    private const string PopupName = "Mappy_MarkerGroup_Menu";

    private static readonly List<MarkerGroupInfo> groups = [];

    /// <summary>
    /// Refresh the group list for the currently selected map.
    /// Returns true when there is at least one group, which is also used to decide if the button is drawn.
    /// </summary>
    public static unsafe bool HasGroups()
    {
        groups.Clear();
        System.CustomMarkerConfig.CollectGroupsOnMap(AgentMap.Instance()->SelectedMapId, groups);

        return groups.Count > 0;
    }

    public static void Open() => ImGui.OpenPopup(PopupName);

    public static void Draw()
    {
        using var popup = ImRaii.Popup(PopupName);
        if (!popup) return;

        if (groups.Count is 0)
        {
            ImGui.TextDisabled(Localization.Get("Toolbar.NoMarkerGroups", "No marker groups on this map"));
            return;
        }

        var iconSize = ImGuiHelpers.ScaledVector2(18.0f, 18.0f);

        foreach (var group in groups)
        {
            using var id = ImRaii.PushId(group.Id);

            var visible = System.SystemConfig.GetGroupVisibility(group.Id, group.DefaultVisible);
            var texture = Service.TextureProvider.GetFromGameIcon(group.IconId).GetWrapOrEmpty();

            ImGui.Image(texture.Handle, iconSize);
            ImGui.SameLine();

            if (ImGui.MenuItem(group.DisplayName, string.Empty, visible))
            {
                System.SystemConfig.ToggleGroupVisibility(group.Id, group.DefaultVisible);
            }
        }
    }
}
