using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Window;
using Mappy.Controllers;
using Mappy.Data;
using Mappy.Windows;

namespace Mappy.Classes.MapWindowComponents;

public unsafe class MapContextMenu
{
    public void Draw(Vector2 mapDrawOffset)
    {
        using var contextMenu = ImRaii.ContextPopup("Mappy_Context_Menu");

        if (!contextMenu) return;

        if (ImGui.MenuItem(Localization.Get("Context.PlaceFlag", "Place Flag"))) {
            var worldPosition = GetCursorWorldPosition(mapDrawOffset);

            AgentMap.Instance()->FlagMarkerCount = 0;
            AgentMap.Instance()->SetFlagMapMarker(AgentMap.Instance()->SelectedTerritoryId, AgentMap.Instance()->SelectedMapId, worldPosition.X, worldPosition.Y);
            AgentChatLog.Instance()->InsertTextCommandParam(1048, false);
        }

        if (ImGui.MenuItem(Localization.Get("Context.RemoveFlag", "Remove Flag"), false, AgentMap.Instance()->FlagMarkerCount is not 0)) {
            AgentMap.Instance()->FlagMarkerCount = 0;
        }

        if (ImGui.MenuItem(Localization.Get("Context.AddCustomMarker", "Add Custom Marker"), false, AgentMap.Instance()->SelectedMapId is not 0)) {
            AddCustomMarker(mapDrawOffset);
        }

        if (ImGui.MenuItem(Localization.Get("Context.NavigateHere", "Go Here"))) {
            VnavController.NavigateTo(MarkerContextMenu.FromFlatPosition(GetCursorWorldPosition(mapDrawOffset)), fly: false);
        }

        if (ImGui.MenuItem(Localization.Get("Context.FlyHere", "Fly Here"))) {
            VnavController.NavigateTo(MarkerContextMenu.FromFlatPosition(GetCursorWorldPosition(mapDrawOffset)), fly: true);
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.MenuItem(Localization.Get("Context.CenterOnPlayer", "Center on Player"), false, Service.ObjectTable.LocalPlayer is not null) && Service.ObjectTable.LocalPlayer is not null) {
            System.IntegrationsController.OpenOccupiedMap();
            System.MapRenderer.CenterOnGameObject(Service.ObjectTable.LocalPlayer);
        }

        if (ImGui.MenuItem(Localization.Get("Context.CenterOnMap", "Center on Map"))) {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.DrawOffset = Vector2.Zero;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.MenuItem(Localization.Get("Context.LockZoom", "Lock Zoom"), "", ref System.SystemConfig.ZoomLocked)) {
            SystemConfig.Save();
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.MenuItem(Localization.Get("Context.OpenQuestList", "Open Quest List"), false, System.WindowManager.GetWindow<QuestListWindow>() is null)) {
            System.WindowManager.AddWindow(new QuestListWindow(), WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
        }

        if (ImGui.MenuItem(Localization.Get("Context.OpenFateList", "Open Fate List"), false, System.WindowManager.GetWindow<FateListWindow>() is null)) {
            System.WindowManager.AddWindow(new FateListWindow(), WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
        }

        if (ImGui.MenuItem(Localization.Get("Context.OpenFlagList", "Open Flag List"), false, System.WindowManager.GetWindow<FlagHistoryWindow>() is null)) {
            System.WindowManager.AddWindow(new FlagHistoryWindow(), WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
        }
    }

    private static void AddCustomMarker(Vector2 mapDrawOffset)
    {
        var worldPosition = GetCursorWorldPosition(mapDrawOffset);

        // The map only tells us the horizontal position, seed the height with the players own height and let the
        // navigation mesh correct it, so the marker is usable by path finding commands later on.
        var guess = new Vector3(worldPosition.X, Service.ObjectTable.LocalPlayer?.Position.Y ?? 0.0f, worldPosition.Y);
        var position = VnavController.SnapToNavmesh(guess);

        var marker = new CustomMarker
        {
            MapId = AgentMap.Instance()->SelectedMapId,
            X = position.X,
            Y = position.Z,
            Altitude = position.Y,
            Name = $"Marker {worldPosition.X:F1}, {worldPosition.Y:F1}",
        };

        System.CustomMarkerConfig.Markers.Add(marker);
        System.CustomMarkerConfig.Save();

        System.CustomMarkerWindow.Edit(marker);
    }

    private static Vector2 GetCursorWorldPosition(Vector2 mapDrawOffset)
    {
        var cursorPosition = ImGui.GetMousePosOnOpeningCurrentPopup(); // Get initial cursor position (screen relative)
        var textureClickLocation = (cursorPosition - mapDrawOffset - System.MapRenderer.DrawPosition) / MapRenderer.MapRenderer.Scale; // Math
        var mapCenterRelative = textureClickLocation - new Vector2(1024.0f, 1024.0f); // One of our vectors made the map centered, undo it.

        return mapCenterRelative / DrawHelpers.GetMapScaleFactor() + DrawHelpers.GetRawMapOffsetVector(); // Apply offset x/y and scalefactor
    }
}
