using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Ipc;

namespace Mappy.Controllers;

/// <summary>
/// Thin wrapper around the vnavmesh plugin.
/// Everything is optional, all entry points degrade gracefully when vnavmesh is missing or its navmesh is not loaded yet.
/// </summary>
public static class VnavController
{
    /// <summary>
    /// Vertical search radii used when navigating, the height of a marker is frequently just a guess.
    /// </summary>
    private static readonly float[] VerticalSearchSteps = [5.0f, 50.0f, 200.0f, 500.0f];

    private static ICallGateSubscriber<bool>? isReady;
    private static ICallGateSubscriber<Vector3, float, float, Vector3?>? nearestPoint;
    private static ICallGateSubscriber<object>? stop;

    private static void EnsureInitialized()
    {
        isReady ??= Service.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        nearestPoint ??= Service.PluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        stop ??= Service.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
    }

    /// <summary>
    /// Abort any running pathfinding, used when something else takes over the movement (a teleport for example),
    /// otherwise the character keeps walking to the old destination afterwards.
    /// </summary>
    public static void Stop()
    {
        try
        {
            EnsureInitialized();
            stop!.InvokeAction();
        }
        catch
        {
            // vnavmesh not installed, nothing is moving anyway.
        }
    }

    public static bool IsReady()
    {
        try
        {
            EnsureInitialized();
            return isReady!.InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Snap a position onto the navigation mesh, returns the input when vnavmesh is unavailable or nothing was found nearby.
    /// </summary>
    public static Vector3 SnapToNavmesh(Vector3 position, float horizontalSearch = 5.0f, float verticalSearch = 5.0f)
        => TrySnap(position, horizontalSearch, verticalSearch) ?? position;

    /// <summary>
    /// Snap the position onto the navmesh and hand it to vnavmesh through its text command.
    /// </summary>
    public static bool NavigateTo(Vector3 position, bool fly)
    {
        if (!IsReady())
        {
            Service.ChatGui.Print($"[Mappy] {Localization.Get("Vnav.NotReady", "vnavmesh is not ready, can not navigate.")}");
            return false;
        }

        var target = SnapForNavigation(position);
        var coordinates = string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2}", target.X, target.Y, target.Z);

        return Service.DalamudCommands.ProcessCommand($"/vnav {(fly ? "flyto" : "moveto")} {coordinates}");
    }

    /// <summary>
    /// Widen the vertical search until the mesh is hit, a marker placed from the map only carries a horizontal position
    /// and the height that was assumed for it can be far off.
    /// </summary>
    private static Vector3 SnapForNavigation(Vector3 position)
    {
        foreach (var verticalSearch in VerticalSearchSteps)
        {
            if (TrySnap(position, 5.0f, verticalSearch) is { } snapped)
            {
                return snapped;
            }
        }

        return position;
    }

    private static Vector3? TrySnap(Vector3 position, float horizontalSearch, float verticalSearch)
    {
        try
        {
            EnsureInitialized();
            return nearestPoint!.InvokeFunc(position, horizontalSearch, verticalSearch);
        }
        catch
        {
            return null;
        }
    }
}
