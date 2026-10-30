using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Mappy.Classes;

namespace Mappy.MapRenderer;

public partial class MapRenderer {
    private unsafe void DrawCustomMarkers() {
        if (System.CustomMarkerConfig.BuiltInMarkers.Count is 0 && System.CustomMarkerConfig.Markers.Count is 0) return;

        var selectedMapId = AgentMap.Instance()->SelectedMapId;
        var mapScale = DrawHelpers.GetMapScaleFactor();
        var mapOffset = DrawHelpers.GetMapOffsetVector();
        var centerOffset = DrawHelpers.GetMapCenterOffsetVector();

        foreach (var marker in System.CustomMarkerConfig.GetVisibleMarkersForMap(selectedMapId)) {
            var iconId = marker.DisplayIconId;
            if (iconId is 0) continue;

            var popupSeed = MarkerContextMenu.GetPopupSeed(iconId, marker.X, marker.Y);

            // Same conversion the game uses for its own markers, marker.X/Y are world coordinates.
            var position = (marker.Position * mapScale - mapOffset + centerOffset) * Scale;

            DrawHelpers.DrawMapMarker(new MarkerInfo {
                Position = position,
                Offset = DrawPosition,
                Scale = Scale,
                IconId = iconId,
                IconScaleMultiplier = marker.DisplayScale,
                AlwaysAllowInteraction = true,
                PrimaryText = marker.PrimaryText,
                SecondaryText = marker.SecondaryText,
                OnLeftClicked = marker.ClickAction,
                PopupSeed = popupSeed,
            });

            MarkerContextMenu.Draw(popupSeed, marker.GetWorldPosition(), position + DrawPosition, marker);
        }
    }
}
