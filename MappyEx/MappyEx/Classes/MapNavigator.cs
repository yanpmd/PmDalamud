using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Mappy.Classes;

/// <summary>
/// Opens a map for the browser and search UI in one place, so both show the map the same way.
/// </summary>
public static class MapNavigator
{
    public static unsafe void Show(uint mapId, Vector2? texturePosition)
    {
        if (mapId is 0) return;

        if (AgentMap.Instance()->SelectedMapId != mapId) {
            System.IntegrationsController.OpenMap(mapId);
        }

        if (texturePosition is not { } position) return;

        // The position is the reason the map is shown, do not let following the player pull the view away.
        System.SystemConfig.FollowPlayer = false;
        System.MapRenderer.RequestTextureFocus(mapId, position);
    }
}
