using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Extensions;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Mappy.Classes;

namespace Mappy.Controllers;

/// <summary>
/// Lifestream only accepts an aethernet teleport while the player stands near an aetheryte or one of its shards.
/// When a destination is clicked from out of range, this walks the player to the nearest usable point of the same
/// city and asks Lifestream again once the player is in range, instead of silently failing.
/// </summary>
public static class AethernetNavigator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The game accepts the aetheryte interaction a few meters away from it, walking precisely onto it is not needed
    /// and vnavmesh often stops next to it.
    /// </summary>
    private const float ArrivalDistance = 5.0f;

    private static uint pendingAetheryteRowId;
    private static Vector3 pendingWalkTarget;
    private static uint startedTerritory;
    private static DateTime startedAt;

    public static bool IsRunning => pendingAetheryteRowId is not 0;

    /// <summary>
    /// Walk to the closest point of the destination city and teleport to the destination once in range.
    /// When that point already is the destination the player is simply sent there and no teleport follows.
    /// </summary>
    /// <param name="placeNameRowId">destination shard, matches the marker data key</param>
    /// <param name="navigated">true when a walk was started, independent of whether a teleport follows</param>
    /// <returns>true when a teleport is queued for after arrival</returns>
    public static bool Begin(uint placeNameRowId, out bool navigated)
    {
        navigated = false;
        Cancel();

        if (Service.ObjectTable.LocalPlayer is not { } player) return false;

        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();
        if (aetheryteSheet.FirstOrNull(entry => entry.AethernetName.RowId == placeNameRowId) is not { } destination) return false;

        if (!TryFindNearestSource(destination.AethernetGroup, player.Position, out var walkSource, out var walkTarget))
        {
            walkSource = FindNearestSourceFromSheet(aetheryteSheet, destination.AethernetGroup, player.Position);
            if (walkSource is not { } fallback) return false;
            if (GetClosestLevelPosition(fallback, player.Position) is not { } fallbackPosition) return false;

            walkTarget = fallbackPosition;
        }

        var walkSourceId = walkSource?.RowId ?? 0;

        // Already closest to the destination, walking there completes the request on its own.
        if (walkSourceId == destination.RowId)
        {
            Service.Log.Information($"[Aethernet] nearest point is the destination at {walkTarget}, walking there directly");
            VnavController.NavigateTo(walkTarget, fly: false);

            navigated = true;

            return false;
        }

        var distance = Vector3.Distance(player.Position, walkTarget);

        Service.Log.Information($"[Aethernet] walking to source {walkSourceId} at {walkTarget} ({distance:F0}y) before teleporting to {destination.RowId}");
        VnavController.NavigateTo(walkTarget, fly: false);
        QueueTeleportAfterArrival(walkTarget, destination.RowId);

        navigated = true;

        return true;
    }

    /// <summary>
    /// Queue a teleport that fires once the player reaches <paramref name="walkTarget"/>.
    /// Kept separate from <see cref="Begin"/> because the point the player walks to and the teleport target can differ,
    /// a big aetheryte for example is a valid teleport destination but is reached through a nearby shard.
    /// </summary>
    public static void QueueTeleportAfterArrival(Vector3 walkTarget, uint teleportTargetRowId)
    {
        pendingAetheryteRowId = teleportTargetRowId;
        pendingWalkTarget = walkTarget;
        startedTerritory = Service.ClientState.TerritoryType;
        startedAt = DateTime.UtcNow;
    }

    public static void Cancel()
    {
        pendingAetheryteRowId = 0;
        pendingWalkTarget = default;
        startedTerritory = 0;
        startedAt = default;
    }

    /// <summary>
    /// Called every frame, issues the queued teleport as soon as the player is close enough to the walk target.
    /// </summary>
    public static void Update()
    {
        if (pendingAetheryteRowId is 0) return;

        if (Service.Condition[ConditionFlag.InCombat] ||
            Service.ClientState.TerritoryType != startedTerritory ||
            DateTime.UtcNow - startedAt > Timeout)
        {
            Service.Log.Information("[Aethernet] cancelled, combat, area change or timeout");
            Cancel();
            VnavController.Stop();

            return;
        }

        // The player is still travelling.
        if (Service.Condition[ConditionFlag.BetweenAreas]) return;

        if (Service.ObjectTable.LocalPlayer is not { } player) return;

        // GetActiveAetheryte does not update reliably while the map is open, so the distance is the primary check.
        var inRange = LifestreamController.GetActiveAetheryte() is not 0 ||
                      Vector3.Distance(player.Position, pendingWalkTarget) <= ArrivalDistance;

        if (!inRange) return;

        var destination = pendingAetheryteRowId;
        var distance = Vector3.Distance(player.Position, pendingWalkTarget);

        Cancel();

        // The walk was only a means to get in range, stop it so the player does not keep running after the teleport.
        VnavController.Stop();

        Service.Log.Information($"[Aethernet] arrived ({distance:F1}y), teleporting to {destination}");
        LifestreamController.TryAethernetTeleportById(destination, out _);
    }

    /// <summary>
    /// Nearest point of the group that the player can actually walk to.
    /// Both shards (data type 4, key is a PlaceName row) and big aetherytes (data type 3, key is an Aetheryte row)
    /// qualify, the game accepts the interaction from either of them.
    /// </summary>
    public static unsafe bool TryFindNearestSource(uint aethernetGroup, Vector3 position, out Aetheryte? source, out Vector3 sourcePosition)
    {
        source = null;
        sourcePosition = default;

        var agent = AgentMap.Instance();
        if (agent is null) return false;

        var mapScale = DrawHelpers.GetMapScaleFactor();
        if (mapScale is 0.0f) return false;

        var mapOffset = DrawHelpers.GetRawMapOffsetVector();
        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();

        var found = false;
        var nearestDistance = float.MaxValue;

        for (var index = 0; index < agent->MapMarkerCount; index++)
        {
            ref var marker = ref agent->MapMarkers[index];

            // Copy out of the reference first, a ref local can not be captured by the lookups below.
            var dataType = marker.DataType;
            var dataKey = marker.DataKey;
            var markerPosition = new Vector2(marker.MapMarker.X, marker.MapMarker.Y);

            Aetheryte? candidate = null;

            if (dataType is 4)
            {
                candidate = aetheryteSheet.FirstOrNull(entry => entry.AethernetName.RowId == dataKey);
            }
            else if (dataType is 3)
            {
                var row = aetheryteSheet.GetRow(dataKey);
                if (row.RowId is not 0) candidate = row;
            }

            if (candidate is not { } candidateRow) continue;
            if (candidateRow.AethernetGroup != aethernetGroup) continue;

            var flat = markerPosition / 16.0f / mapScale + mapOffset;
            var candidatePosition = new Vector3(flat.X, position.Y, flat.Y);

            var distance = Vector3.Distance(position, candidatePosition);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            source = candidateRow;
            sourcePosition = candidatePosition;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Fallback when the map does not expose any usable marker, for example while the map is still loading.
    /// </summary>
    private static Aetheryte? FindNearestSourceFromSheet(ExcelSheet<Aetheryte> aetheryteSheet, uint aethernetGroup, Vector3 playerPosition)
    {
        Aetheryte? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var candidate in aetheryteSheet)
        {
            if (candidate.AethernetGroup != aethernetGroup) continue;

            if (GetClosestLevelPosition(candidate, playerPosition) is not { } candidatePosition) continue;

            var distance = Vector3.Distance(playerPosition, candidatePosition);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    /// <summary>
    /// Position of an aetheryte as the player sees it on the map.
    /// The level rows of the sheet do not necessarily point at the object itself, so the marker the game draws is used
    /// for the horizontal position and the sheet only for the height.
    /// </summary>
    public static unsafe Vector3? ResolvePosition(Aetheryte shard, Vector3 playerPosition)
    {
        var levelPosition = GetClosestLevelPosition(shard, playerPosition);

        if (TryGetMapMarkerPosition(shard.AethernetName.RowId, shard.RowId) is { } mapPosition) {
            return new Vector3(mapPosition.X, levelPosition?.Y ?? playerPosition.Y, mapPosition.Y);
        }

        return levelPosition;
    }

    /// <summary>
    /// Turn the position of the marker the game draws for an aetheryte into world coordinates,
    /// mirrors the conversion used while drawing.
    /// </summary>
    private static unsafe Vector2? TryGetMapMarkerPosition(uint placeNameRowId, uint aetheryteRowId)
    {
        var agent = AgentMap.Instance();
        if (agent is null) return null;

        var mapScale = DrawHelpers.GetMapScaleFactor();
        if (mapScale is 0.0f) return null;

        var mapOffset = DrawHelpers.GetRawMapOffsetVector();

        for (var index = 0; index < agent->MapMarkerCount; index++) {
            ref var marker = ref agent->MapMarkers[index];

            // Shards carry the PlaceName row as key, big aetherytes carry their own row id.
            var matches = marker.DataType switch {
                4 => marker.DataKey == placeNameRowId,
                3 => marker.DataKey == aetheryteRowId,
                _ => false,
            };

            if (!matches) continue;

            return new Vector2(marker.MapMarker.X, marker.MapMarker.Y) / 16.0f / mapScale + mapOffset;
        }

        return null;
    }

    /// <summary>
    /// A shard can reference several level rows, some of which are not the shard itself. Picking the closest one is
    /// the safest way to get a height that is near the player.
    /// </summary>
    private static Vector3? GetClosestLevelPosition(Aetheryte aetheryte, Vector3 playerPosition)
    {
        Vector3? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var levelReference in aetheryte.Level)
        {
            if (levelReference.ValueNullable is not { } level) continue;

            var position = new Vector3(level.X, level.Y, level.Z);
            var distance = Vector3.Distance(playerPosition, position);
            if (distance >= closestDistance) continue;

            closestDistance = distance;
            closest = position;
        }

        return closest;
    }
}
