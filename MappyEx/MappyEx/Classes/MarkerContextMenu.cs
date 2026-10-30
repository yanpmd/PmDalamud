using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Mappy.Controllers;
using Mappy.Data;

namespace Mappy.Classes;

/// <summary>
/// Shared right click menu for map icons.
/// Used by game markers, markers injected by other plugins and user defined markers.
/// </summary>
public static class MarkerContextMenu
{
    private const string PopupName = "##mappy_marker_menu";

    /// <summary>
    /// Stable popup id for a marker, derived from its own data so no string is allocated while drawing.
    /// </summary>
    public static int GetPopupSeed(uint iconId, float x, float y) => HashCode.Combine(iconId, (int)x, (int)y);

    public static void Open(int seed)
    {
        System.MapWindow.MarkerMenuOpened = true;

        ImGui.PushID(seed);
        ImGui.OpenPopup(PopupName);
        ImGui.PopID();
    }

    /// <summary>
    /// Convert a position relative to the rendered map child back into world coordinates.
    /// This mirrors the conversion used while drawing, so it is correct for every marker regardless of how its data is stored.
    /// </summary>
    /// <param name="childRelativePosition">position inside the map child, the draw offset has to be removed first</param>
    public static Vector2 GetWorldPosition(Vector2 childRelativePosition)
        => ((childRelativePosition - System.MapRenderer.DrawPosition) / MapRenderer.MapRenderer.Scale - DrawHelpers.GetMapCenterOffsetVector()) /
           DrawHelpers.GetMapScaleFactor() + DrawHelpers.GetRawMapOffsetVector();

    /// <param name="seed">popup id, see <see cref="GetPopupSeed"/></param>
    /// <param name="knownWorldPosition">exact position when the caller owns it, user defined markers for example</param>
    /// <param name="iconChildRelativePosition">center of the drawn icon, used to derive the world position when no exact one is known</param>
    /// <param name="customMarker">the user defined marker being edited, null for markers that do not belong to Mappy</param>
    public static void Draw(int seed, Vector3? knownWorldPosition, Vector2? iconChildRelativePosition, CustomMarker? customMarker)
    {
        ImGui.PushID(seed);

        try
        {
            if (!ImGui.BeginPopup(PopupName)) return;

            try
            {
                var worldPosition = knownWorldPosition ?? FromFlatPosition(iconChildRelativePosition is { } iconPosition ? GetWorldPosition(iconPosition) : Vector2.Zero);

                if (ImGui.MenuItem(Localization.Get("CustomMarker.MoveTo", "Go To Marker"))) {
                    VnavController.NavigateTo(worldPosition, fly: false);
                }

                if (ImGui.MenuItem(Localization.Get("CustomMarker.FlyTo", "Fly To Marker"))) {
                    VnavController.NavigateTo(worldPosition, fly: true);
                }

                // Built in group markers are read only, they can only be navigated to.
                if (customMarker is { BuiltIn: false }) {
                    if (ImGui.MenuItem(Localization.Get("CustomMarker.Edit", "Edit Marker"))) {
                        System.CustomMarkerWindow.Edit(customMarker);
                    }
                }
            }
            finally
            {
                ImGui.EndPopup();
            }
        }
        finally
        {
            ImGui.PopID();
        }
    }

    /// <summary>
    /// Marker icons only carry a horizontal position, the height is a guess that gets snapped onto the navmesh before navigating.
    /// </summary>
    public static Vector3 FromFlatPosition(Vector2 horizontal)
        => new(horizontal.X, Service.ObjectTable.LocalPlayer?.Position.Y ?? 0.0f, horizontal.Y);
}
