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
using Mappy.Classes;
using Mappy.Data;

namespace Mappy.Windows;

/// <summary>
/// Editor for a single favorite location. One instance is shared by every favorite, edited state lives only here.
/// </summary>
public class FavoriteEditorWindow : Window
{
    private FavoritePoint? editingPoint;
    private bool creatingGroup;
    private string newGroupName = string.Empty;

    public FavoriteEditorWindow() : base(Localization.Get("FavoriteEditor.Window", "Favorite Location"), new Vector2(340.0f, 400.0f))
    {
    }

    public void UpdateLocalizedName()
        => WindowName = $"{Localization.Get("FavoriteEditor.Window", "Favorite Location")}##{Service.PluginInterface.InternalName}";

    public void Edit(FavoritePoint point)
    {
        editingPoint = point;
        creatingGroup = false;
        newGroupName = string.Empty;
        UnCollapseOrShow();
    }

    protected override void DrawContents()
    {
        if (editingPoint is not { } point) {
            ImGuiHelpers.CenteredText(Localization.Get("FavoriteEditor.NoSelection", "No favorite selected"));
            return;
        }

        var coordinate = point.GetMapCoordinate();
        ImGuiTweaks.Header(Localization.F("FavoriteEditor.Located", point.GetMapName(), $"{coordinate.X:F1}", $"{coordinate.Y:F1}"));

        ImGui.TextUnformatted(Localization.Get("FavoriteEditor.Name", "Name"));
        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputText("##favoriteName", ref point.Name, 128);

        ImGui.TextUnformatted(Localization.Get("FavoriteEditor.Group", "Group"));
        ImGui.SetNextItemWidth(-1.0f);
        DrawGroupCombo(point);

        if (creatingGroup) DrawNewGroup(point);

        ImGui.TextUnformatted(Localization.Get("FavoriteEditor.IconId", "Icon ID"));
        var iconId = (int)Math.Min(point.IconId, IconHelper.MaxIconId);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 72.0f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("##favoriteIconId", ref iconId, 1, 10)) {
            point.IconId = IconHelper.Clamp(iconId);
        }

        ImGui.SameLine();

        if (IconHelper.GetTexture(point.IconId) is { } texture) {
            ImGui.Image(texture.Handle, ImGuiHelpers.ScaledVector2(24.0f, 24.0f));
        }
        else {
            ImGui.Dummy(ImGuiHelpers.ScaledVector2(24.0f, 24.0f));
        }

        if (!IconHelper.IsValid(point.IconId)) {
            ImGui.TextColored(KnownColor.Orange.Vector(), Localization.Get("FavoriteEditor.InvalidIconId", "This icon id does not exist."));
        }

        ImGui.TextUnformatted(Localization.Get("FavoriteEditor.Altitude", "Height (world Y)"));
        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputFloat("##favoriteAltitude", ref point.Altitude, 0.1f, 1.0f, "%.2f");
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(Localization.Get("FavoriteEditor.AltitudeHint", "Ground height used by pathfinding. Correct this if navigation lands above or below the spot."));
        }

        ImGui.TextUnformatted(Localization.Get("FavoriteEditor.Note", "Note"));
        ImGui.InputTextMultiline("##favoriteNote", ref point.Note, 512, new Vector2(-1.0f, 55.0f * ImGuiHelpers.GlobalScale));

        DrawButtons(point);
    }

    private void DrawGroupCombo(FavoritePoint point)
    {
        var preview = System.FavoriteConfig.GetGroupName(point.GroupId);

        using var combo = ImRaii.Combo("##favoriteGroupCombo", preview);
        if (!combo) return;

        if (ImGui.Selectable(Localization.Get("FavoriteSidebar.Ungrouped", "Ungrouped"), string.IsNullOrEmpty(point.GroupId))) {
            point.GroupId = string.Empty;
        }

        foreach (var group in System.FavoriteConfig.Groups) {
            if (!ImGui.Selectable(group.Name, point.GroupId == group.Id)) continue;

            point.GroupId = group.Id;
        }

        ImGui.Separator();

        if (ImGui.Selectable(Localization.Get("FavoriteEditor.NewGroup", "New Group..."))) {
            creatingGroup = true;
            newGroupName = string.Empty;
        }
    }

    private void DrawNewGroup(FavoritePoint point)
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 100.0f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##newFavoriteGroup", Localization.Get("FavoriteEditor.GroupNameHint", "Group name"), ref newGroupName, 64);

        ImGui.SameLine();

        if (ImGui.Button(Localization.Get("FavoriteEditor.Create", "Create"))) {
            point.GroupId = System.FavoriteConfig.AddGroup(newGroupName).Id;
            creatingGroup = false;
            newGroupName = string.Empty;
        }

        ImGui.SameLine();

        if (ImGui.Button(Localization.Get("FavoriteEditor.Cancel", "Cancel"))) {
            creatingGroup = false;
        }
    }

    private void DrawButtons(FavoritePoint point)
    {
        var buttonSize = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2.0f) / 3.0f, 24.0f * ImGuiHelpers.GlobalScale);

        ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - buttonSize.Y);

        if (ImGui.Button(Localization.Get("FavoriteEditor.Delete", "Delete"), buttonSize)) {
            System.FavoriteConfig.Remove(point);
            editingPoint = null;
            Close();

            return;
        }

        ImGui.SameLine();

        if (ImGui.Button(Localization.Get("FavoriteEditor.Save", "Save"), buttonSize)) {
            System.FavoriteConfig.Save();
            Close();

            return;
        }

        ImGui.SameLine();

        if (ImGui.Button(Localization.Get("FavoriteEditor.Center", "Center"), buttonSize)) {
            System.SystemConfig.FollowPlayer = false;
            System.IntegrationsController.OpenMap(point.MapId);
            System.MapRenderer.CenterOnCoordinate(point.Position);
        }
    }

    public override void OnClose()
    {
        editingPoint = null;
        creatingGroup = false;
        System.FavoriteConfig?.Save();
    }
}
