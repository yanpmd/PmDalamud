using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.Interop;
using KamiLib.Extensions;
using KamiLib.Window;
using Lumina.Excel.Sheets;
using Mappy.Windows;
using Action = System.Action;

namespace Mappy.Classes.MapWindowComponents;

/// <summary>
/// Describes one toolbar button as data, so the layout loop stays uniform and adding a button is a single line.
/// </summary>
internal readonly record struct ToolbarButton(
    FontAwesomeIcon Icon,
    string Id,
    Func<string> Tooltip,
    Action OnClick,
    Func<bool>? IsActive = null,
    Func<bool>? IsVisible = null);

public unsafe class MapToolbar {
    /// <summary>
    /// Buttons of the left cluster, built once. The delegates live for the lifetime of the plugin,
    /// so drawing the toolbar does not allocate.
    /// </summary>
    private static readonly ToolbarButton[] Buttons = [
        new(FontAwesomeIcon.ArrowUp, "up",
            () => Localization.Get("Toolbar.OpenParentMap", "Open Parent Map"), OpenParentMap),

        new(FontAwesomeIcon.LayerGroup, "layers",
            () => Localization.Get("Toolbar.ShowMapLayers", "Show Map Layers"), ShowLayers),

        new(FontAwesomeIcon.LocationArrow, "follow",
            () => Localization.Get("Toolbar.ToggleFollowPlayer", "Toggle Follow Player"), ToggleFollowPlayer,
            () => System.SystemConfig.FollowPlayer),

        new(FontAwesomeIcon.ArrowsToCircle, "centerPlayer",
            () => Localization.Get("Toolbar.CenterOnPlayer", "Center on Player"), CenterOnPlayer),

        new(FontAwesomeIcon.MapMarked, "centerMap",
            () => Localization.Get("Toolbar.CenterOnMap", "Center on Map"), CenterOnMap),

        new(FontAwesomeIcon.Search, "search",
            () => Localization.Get("Toolbar.SearchForMap", "Search for Map"), OpenSearchWindow),

        // Only shown when the selected map actually has marker groups.
        new(FontAwesomeIcon.ListUl, "markerGroups",
            () => Localization.Get("Toolbar.MarkerGroups", "Marker Groups"), MarkerGroupMenu.Open,
            IsVisible: MarkerGroupMenu.HasGroups),
    ];

    public void Draw() {
        var borderPadding = 2.5f;
        var toolbarSize = new Vector2(ImGui.GetContentRegionMax().X, 33.0f * ImGuiHelpers.GlobalScale);

        using var childBackgroundStyle =
            ImRaii.PushColor(ImGuiCol.ChildBg, Vector4.Zero with { W = System.SystemConfig.ToolbarFade });
        using var toolbarChild = ImRaii.Child("toolbar_child", toolbarSize);
        if (!toolbarChild) return;

        ImGui.SetCursorPos(new Vector2(borderPadding, borderPadding) * ImGuiHelpers.GlobalScale);

        var hasPreviousButton = false;

        foreach (var button in Buttons) {
            if (button.IsVisible?.Invoke() is false) continue;

            if (hasPreviousButton) ImGui.SameLine();

            DrawButton(button);
            hasPreviousButton = true;
        }

        // Need to run right after the buttons, both popups are opened during this frame.
        DrawLayersContextMenu();
        MarkerGroupMenu.Draw();

        var offset = 17f * ImGuiHelpers.GlobalScale + ImGui.GetStyle().FramePadding.X * 2;
        if (System.SystemConfig.HideWindowFrame)
            offset *= 2.0f;

        DrawRightButtons(offset, borderPadding);
    }

    private static void DrawButton(ToolbarButton button) {
        var isActive = button.IsActive?.Invoke() ?? false;

        using var color = ImRaii.PushColor(ImGuiCol.Button, ImGui.GetStyle().GetColor(ImGuiCol.ButtonActive), isActive);

        if (MappyGuiTweaks.IconButton(button.Icon, button.Id, button.Tooltip())) {
            button.OnClick();
        }
    }

    private static void DrawRightButtons(float offset, float borderPadding) {
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - offset - ImGui.GetStyle().ItemSpacing.X -
                            ImGuiHelpers.GlobalScale * borderPadding);

        if (MappyGuiTweaks.IconButton(FontAwesomeIcon.Cog, "settings", Localization.Get("Toolbar.OpenSettings", "Open Settings"))) {
            System.ConfigWindow.UnCollapseOrShow();
            ImGui.SetWindowFocus(System.ConfigWindow.WindowName);
        }

        if (System.SystemConfig.HideWindowFrame) {
            ImGui.SameLine();

            if (MappyGuiTweaks.IconButton(FontAwesomeIcon.Times, "closeMap", Localization.Get("Toolbar.CloseMap", "Close Map"))) {
                System.MapWindow.Close();
            }
        }
    }

    private static void OpenParentMap() {
        using var valueArgs = new RentedAtkValues(1);
        valueArgs[0].SetInt(5);

        using var returnValue = new RentedAtkValues(1);
        AgentMap.Instance()->ReceiveEvent(returnValue, valueArgs, 1, 0);
    }

    private static void ShowLayers() => ImGui.OpenPopup("Mappy_Show_Layers");

    private static void ToggleFollowPlayer() {
        System.SystemConfig.FollowPlayer = !System.SystemConfig.FollowPlayer;

        if (System.SystemConfig.FollowPlayer) {
            System.IntegrationsController.OpenOccupiedMap();
        }
    }

    private static void CenterOnPlayer() {
        if (Service.ObjectTable.LocalPlayer is not { } player) return;

        // Nothing to do while the map is already following the player.
        if (System.SystemConfig.FollowPlayer) return;

        System.IntegrationsController.OpenOccupiedMap();
        System.MapRenderer.CenterOnGameObject(player);
    }

    private static void CenterOnMap() {
        System.SystemConfig.FollowPlayer = false;
        System.MapRenderer.DrawOffset = Vector2.Zero;
    }

    private static void OpenSearchWindow() {
        System.WindowManager.AddWindow(new MapSelectionWindow {
            SingleSelectionCallback = selection => {
                if (selection?.Map != null) {
                    if (AgentMap.Instance()->SelectedMapId != selection.Map.RowId) {
                        System.IntegrationsController.OpenMap(selection.Map.RowId);
                    }

                    if (selection.MarkerLocation is { } location) {
                        System.SystemConfig.FollowPlayer = false;
                        System.MapRenderer.DrawOffset = -location + DrawHelpers.GetMapCenterOffsetVector();
                    }
                }
            },
        }, WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
    }

    private void DrawLayersContextMenu() {
        using var contextMenu = ImRaii.Popup("Mappy_Show_Layers");
        if (!contextMenu) return;

        var currentMap = Service.DataManager.GetExcelSheet<Map>().GetRow(AgentMap.Instance()->SelectedMapId);
        if (currentMap.RowId is 0) return;

        // If this is a region map
        if (currentMap.MapType.RowId == 3) {
            foreach (var marker in AgentMap.Instance()->MapMarkers) {
                if (!DrawHelpers.IsRegionIcon(marker.MapMarker.IconId)) continue;

                var label = marker.MapMarker.Subtext.AsDalamudSeString();

                if (ImGui.MenuItem(label.ToString())) {
                    System.IntegrationsController.OpenMap(marker.DataKey);
                    System.SystemConfig.FollowPlayer = false;
                    System.MapRenderer.DrawOffset = Vector2.Zero;
                }
            }
        }

        // Any other map
        else {
            var layers = Service.DataManager.GetExcelSheet<Map>()
                .Where(eachMap => eachMap.PlaceName.RowId == currentMap.PlaceName.RowId)
                .Where(eachMap => eachMap.MapIndex != 0)
                .OrderBy(eachMap => eachMap.MapIndex)
                .ToList();

            if (layers.Count is 0) {
                ImGui.Text(Localization.Get("Toolbar.NoLayers", "No layers for this map"));
            }

            foreach (var layer in layers) {
                if (ImGui.MenuItem(layer.PlaceNameSub.Value.Name.ExtractText(), "",
                        AgentMap.Instance()->SelectedMapId == layer.RowId)) {
                    System.IntegrationsController.OpenMap(layer.RowId);
                    System.SystemConfig.FollowPlayer = false;
                    System.MapRenderer.DrawOffset = Vector2.Zero;
                }
            }
        }
    }
}
