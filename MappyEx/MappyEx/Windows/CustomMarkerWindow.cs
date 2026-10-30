using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Classes;
using KamiLib.Extensions;
using KamiLib.Window;
using Mappy.Data;

namespace Mappy.Windows;

/// <summary>
/// Editor for user defined markers, only one instance lives at a time and it always edits the marker that was handed to it.
/// </summary>
public class CustomMarkerWindow : Window
{
    private CustomMarker? editingMarker;

    public CustomMarkerWindow() : base(Localization.Get("Window.CustomMarker", "Custom Map Marker"), new Vector2(340.0f, 540.0f))
    {
    }

    public void UpdateLocalizedName()
        => WindowName = $"{Localization.Get("Window.CustomMarker", "Custom Map Marker")}##{Service.PluginInterface.InternalName}";

    public void Edit(CustomMarker marker)
    {
        editingMarker = marker;
        UnCollapseOrShow();
    }

    protected override void DrawContents()
    {
        if (editingMarker is not { } marker) {
            ImGuiHelpers.CenteredText(Localization.Get("CustomMarker.NoSelection", "No marker selected"));
            return;
        }

        var coordinate = marker.GetMapCoordinate();
        ImGuiTweaks.Header(Localization.F("CustomMarker.Located", marker.GetMapName(), $"{coordinate.X:F1}", $"{coordinate.Y:F1}"));

        ImGui.TextUnformatted(Localization.Get("CustomMarker.Name", "Name"));
        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputText("##customMarkerName", ref marker.Name, 128);

        ImGui.TextUnformatted(Localization.Get("CustomMarker.Group", "Group"));
        ImGui.SetNextItemWidth(-1.0f);
        DrawGroupCombo(marker);

        ImGui.TextUnformatted(Localization.Get("CustomMarker.Note", "Note"));
        ImGui.InputTextMultiline("##customMarkerNote", ref marker.Note, 512, new Vector2(-1.0f, 55.0f * ImGuiHelpers.GlobalScale));

        // Grouped markers take both icon and size from their group, so there is nothing to configure here.
        if (marker.IsIconLocked) {
            ImGui.TextDisabled(Localization.Get("CustomMarker.GroupControlsIcon", "Icon and size are defined by the group."));
        }
        else {
            ImGui.TextUnformatted(Localization.Get("CustomMarker.IconId", "Icon ID"));
            var iconId = (int)marker.IconId;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 72.0f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("##customMarkerIconId", ref iconId, 1, 10)) {
                marker.IconId = (uint)Math.Max(1, iconId);
            }

            ImGui.SameLine();

            if (marker.IconId is not 0) {
                var texture = Service.TextureProvider.GetFromGameIcon(marker.IconId).GetWrapOrEmpty();
                ImGui.Image(texture.Handle, ImGuiHelpers.ScaledVector2(24.0f, 24.0f));
            }

            ImGui.TextUnformatted(Localization.Get("CustomMarker.Scale", "Scale"));
            ImGui.SetNextItemWidth(-1.0f);
            ImGui.DragFloat("##customMarkerScale", ref marker.Scale, 0.01f, 0.1f, 5.0f);
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.Checkbox(Localization.Get("CustomMarker.FlagOnClick", "Place flag when clicked"), ref marker.FlagOnClick);

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextUnformatted(Localization.Get("CustomMarker.Commands", "Extra text commands"));
        ImGui.InputTextMultiline("##customMarkerCommands", ref marker.Commands, 2048, new Vector2(-1.0f, 70.0f * ImGuiHelpers.GlobalScale));
        ImGui.TextColored(KnownColor.Gray.Vector(), Localization.Get("CustomMarker.CommandsHint", "One command per line"));

        DrawButtons(marker);
    }

    private static void DrawGroupCombo(CustomMarker marker)
    {
        var preview = MarkerGroups.Resolve(marker.GroupId)?.DisplayName ?? Localization.Get("CustomMarker.NoGroup", "None");

        using var combo = ImRaii.Combo("##customMarkerGroupCombo", preview);
        if (!combo) return;

        if (ImGui.Selectable(Localization.Get("CustomMarker.NoGroup", "None"), marker.GroupId.Length is 0)) {
            marker.GroupId = string.Empty;
        }

        // Only user created groups can be joined, the built in ones are reserved for the markers that ship with the plugin.
        foreach (var group in System.CustomMarkerConfig.CustomGroups) {
            if (!ImGui.Selectable(group.Name, marker.GroupId == group.Id)) continue;

            // Joining a group pins the icon to the icon of that group, the group size is applied while drawing.
            marker.GroupId = group.Id;
            marker.IconId = group.IconId;
            marker.Scale = group.Scale;
        }

        if (System.CustomMarkerConfig.CustomGroups.Count is 0) {
            ImGui.TextDisabled(Localization.Get("CustomGroup.Empty", "No custom groups yet"));
        }
    }

    private void DrawButtons(CustomMarker marker)
    {
        var buttonSize = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2.0f) / 3.0f, 24.0f * ImGuiHelpers.GlobalScale);

        ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - buttonSize.Y);

        if (ImGui.Button(Localization.Get("CustomMarker.Delete", "Delete"), buttonSize)) {
            System.CustomMarkerConfig.Markers.Remove(marker);
            System.CustomMarkerConfig.Save();

            editingMarker = null;
            Close();
            return;
        }

        ImGui.SameLine();

        if (ImGui.Button(Localization.Get("CustomMarker.Save", "Save"), buttonSize)) {
            System.CustomMarkerConfig.Save();
            Close();
            return;
        }

        ImGui.SameLine();

        if (ImGui.Button(Localization.Get("CustomMarker.Center", "Center"), buttonSize)) {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.CenterOnCoordinate(marker.Position);
        }
    }

    public override void OnClose()
    {
        editingMarker = null;
        System.CustomMarkerConfig.Save();
    }
}
