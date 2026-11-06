using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility;
using KamiLib.Extensions;
using Lumina.Excel.Sheets;
using Mappy.Data;

namespace Mappy.Controllers;

/// <summary>
/// Lifestream only accepts an aethernet teleport while the player stands near an aetheryte or one of its shards.
/// This drives the "walk to a shard, teleport, walk on" chain, and keeps the bookkeeping for a cross map teleport
/// that still needs an aethernet hop once the new map is loaded.
/// </summary>
public static class AethernetNavigator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan TeleportStartupDelay = TimeSpan.FromSeconds(1.0);

    /// <summary>
    /// Lifestream can keep its busy flag set after a teleport, never wait longer than this before walking anyway.
    /// </summary>
    private static readonly TimeSpan TeleportBusyTimeout = TimeSpan.FromSeconds(6.0);

    /// <summary>
    /// How long the game may need to load the destination area and its navmesh after a teleport was issued.
    /// </summary>
    private static readonly TimeSpan AreaLoadTimeout = TimeSpan.FromSeconds(10.0);

    /// <summary>
    /// How long a movement waits for the navmesh of the area it was queued in.
    /// </summary>
    private static readonly TimeSpan NavmeshTimeout = TimeSpan.FromSeconds(10.0);

    /// <summary>
    /// The game accepts the aetheryte interaction a few meters away from it, walking precisely onto it is not needed
    /// and vnavmesh often stops next to it. A big aetheryte is a much larger object, so vnavmesh keeps a wider
    /// distance from it and needs its own, larger threshold.
    /// </summary>
    private const float ImmediateTeleportDistance = 4.0f;
    private const float ShardStopDistance = 6.9f;
    private const float AetheryteStopDistance = 10.9f;

    /// <summary>
    /// Icons the game uses for aetherytes in the map marker sheet, the second one shows up on some maps.
    /// </summary>
    private const ushort AetheryteIcon = 60453;
    private const ushort AetheryteIconAlt = 60959;

    private static uint pendingAetheryteRowId;
    private static Vector3 pendingWalkTarget;
    private static float pendingStopDistance = ShardStopDistance;
    private static uint startedTerritory;
    private static DateTime startedAt;

    /// <summary>
    /// Where to walk after the queued teleport finished, lets one click chain "teleport, then walk the rest".
    /// </summary>
    private static Vector3? pendingFinalTarget;
    private static bool pendingFinalFly;
    private static DateTime teleportIssuedAt;
    private static DateTime lastFinalWalkLog;

    /// <summary>
    /// Used to log the arrival check only when it flips instead of every frame.
    /// </summary>
    private static bool lastReportedInRange;

    /// <summary>
    /// Whether the loading screen showed up after the teleport was issued. A hop inside the city does not load a new
    /// area, so the screen is the only reliable sign that the teleport really started.
    /// </summary>
    private static bool sawAreaChange;

    private static readonly TimeSpan CrossMapTimeout = TimeSpan.FromSeconds(60);
    private static uint pendingCrossMapPlaceNameId;
    private static Vector3? pendingCrossMapTarget;
    private static bool pendingCrossMapFly;
    private static uint crossMapOriginTerritory;
    private static DateTime crossMapStartedAt;

    public static bool IsRunning => pendingAetheryteRowId is not 0;

    /// <summary>
    /// Navigate to an arbitrary point: teleport to the shard closest to it, then walk the remaining distance.
    /// Targets on another map first teleport into that map and continue from there.
    /// Plain walking is used whenever no usable shard exists or Lifestream refused the request.
    /// </summary>
    /// <param name="target">world position to end up at</param>
    /// <param name="mapId">Map row the target belongs to, 0 when unknown</param>
    /// <param name="fly">whether the final walking leg should fly</param>
    public static void NavigateToMarker(Vector3 target, uint mapId, bool fly)
    {
        Cancel();

        if (Service.ObjectTable.LocalPlayer is not { } player) return;
        if (Service.Condition[ConditionFlag.InCombat]) return;

        // Aethernet shortcuts are optional, walking plus a mount is the plain vnavmesh route.
        if (!System.SystemConfig.EnableAethernetNavigation)
        {
            VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

            return;
        }

        // The aethernet can only be used through Lifestream, without it the whole trip is walked.
        if (!LifestreamController.IsInstalled())
        {
            Service.Log.Information("[Aethernet] Lifestream is not installed, walking the whole way");
            VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

            return;
        }

        // The target lives in another territory. A city is split over several territories that share one aethernet
        // group, those are the same place and must not trigger a teleport.
        var targetTerritory = mapId is 0 ? 0 : Service.DataManager.GetExcelSheet<Map>().GetRow(mapId).TerritoryType.RowId;
        var targetGroup = targetTerritory is 0 ? 0 : FindAethernetGroupForTerritory(targetTerritory);
        var currentTerritory = Service.ClientState.TerritoryType;

        // A wilderness zone is not part of any city network, it has no shards to hop between and its crystals are
        // plain teleports. The intra city logic below must never apply to it.
        var targetIsWilderness = targetTerritory is not 0 && targetGroup is 0;

        if (targetTerritory is not 0 && targetTerritory != currentTerritory)
        {
            var currentGroup = FindAethernetGroupForTerritory(currentTerritory);

            if (targetIsWilderness || targetGroup != currentGroup)
            {
                Service.Log.Information($"[Aethernet] territory {targetTerritory} (group {targetGroup}) differs from {currentTerritory} (group {currentGroup}), teleporting over");

                if (TryTeleportToTerritory(mapId, target, fly)) return;

                VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

                return;
            }

            Service.Log.Information($"[Aethernet] territory {targetTerritory} shares group {targetGroup} with the player, treating it as the same city");
        }

        // The target sits in the wilderness zone the player is already in, there is no crystal to hop to, just walk.
        if (targetIsWilderness)
        {
            Service.Log.Information($"[Aethernet] wilderness target {targetTerritory} in the current territory, walking the whole way");
            VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

            return;
        }

        // The crystal has to belong to the destination's own city and territory. Every territory has its own coordinate
        // space and those overlap numerically, so a plain distance search happily picks the crystal of a foreign city,
        // which then teleports the player to that city.
        if (!TryFindCrystalNearestTo(target, targetTerritory, targetGroup, out var crystal, out var crystalPosition, out var crystalDistance) ||
            crystalDistance > System.SystemConfig.AethernetSearchRadius)
        {
            Service.Log.Information($"[Aethernet] no usable crystal near {target}, walking the whole way");
            VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

            return;
        }

        if (crystal is not { } crystalRow) return;

        Service.Log.Information($"[Aethernet] crystal {crystalRow.RowId} ({GetAetheryteName(crystalRow)}) at {crystalPosition} is the closest to {target}");

        // Standing in range already, teleport and walk the rest.
        if (LifestreamController.GetActiveAetheryte() is not 0)
        {
            var teleportStarted = TryTeleportToCrystal(crystalRow, out var available);

            if (available && teleportStarted)
            {
                QueueFinalWalk(target, fly);
                Service.Log.Information($"[Aethernet] teleporting to {GetAetheryteName(crystalRow)}, then walking to {target}");

                return;
            }

            // Installed but refused, most likely a crystal of another city, walking is all that is left.
            if (available)
            {
                Service.Log.Information($"[Aethernet] teleport to {crystalRow.RowId} refused, walking to {target}");
                VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

                return;
            }
        }

        if (!TryFindNearestSource(crystalRow.AethernetGroup, player.Position, out var walkSource, out var walkTarget))
        {
            VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

            return;
        }

        // The closest point the player can walk to is that crystal, which means the target already sits next to it.
        if (walkSource?.RowId == crystalRow.RowId)
        {
            Service.Log.Information($"[Aethernet] crystal {crystalRow.RowId} is also the closest walkable point, walking to {target}");
            VnavController.NavigateToWhenReady(target, fly, NavmeshTimeout);

            return;
        }

        Service.Log.Information($"[Aethernet] walking to {walkTarget}, teleporting to {GetAetheryteName(crystalRow)}, then walking to {target}");
        VnavController.NavigateTo(walkTarget, fly: false);
        QueueTeleportAfterArrival(walkTarget, crystalRow.RowId, walkSource, target, fly);
    }

    /// <summary>
    /// Walk to the closest point of the destination city and teleport to the destination once in range.
    /// When that point already is the destination the player is simply sent there and no teleport follows.
    /// </summary>
    /// <param name="placeNameRowId">destination shard, matches the marker data key</param>
    /// <param name="navigated">true when something was started, independent of whether a teleport follows</param>
    /// <returns>true when a teleport is queued for after arrival</returns>
    public static bool Begin(uint placeNameRowId, out bool navigated)
    {
        navigated = false;
        Cancel();

        if (Service.ObjectTable.LocalPlayer is not { } player) return false;

        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();

        // A shard carries the PlaceName row in its aethernet name, a big aetheryte can carry the same name, so the
        // shard is preferred to make sure the teleport resolves to the aethernet and not to the city gate.
        var destination = aetheryteSheet.FirstOrNull(entry => entry.AethernetName.RowId == placeNameRowId && !entry.IsAetheryte)
                          ?? aetheryteSheet.FirstOrNull(entry => entry.AethernetName.RowId == placeNameRowId);

        if (destination is not { } destinationRow) return false;

        // A crystal without an aethernet group is not part of any city network, there are no shards to hop to.
        if (destinationRow.AethernetGroup is 0) return false;

        if (!TryFindNearestSource(destinationRow.AethernetGroup, player.Position, out var walkSource, out var walkTarget)) return false;

        var distance = Vector3.Distance(player.Position, walkTarget);

        // Standing on it already, teleport right away instead of requesting a zero length walk.
        if (distance <= ImmediateTeleportDistance)
        {
            Service.Log.Information($"[Aethernet] already at {walkTarget} ({distance:F1}y), teleporting to {destinationRow.RowId}");
            LifestreamController.TryAethernetTeleportById(destinationRow.RowId, out _);

            navigated = true;

            return false;
        }

        // Already closest to the destination, walking there completes the request on its own.
        if (walkSource?.RowId == destinationRow.RowId)
        {
            Service.Log.Information($"[Aethernet] nearest point is the destination at {walkTarget}, walking there directly");
            VnavController.NavigateTo(walkTarget, fly: false);

            navigated = true;

            return false;
        }

        Service.Log.Information($"[Aethernet] walking to source {walkSource?.RowId} at {walkTarget} ({distance:F0}y) before teleporting to {destinationRow.RowId}");
        VnavController.NavigateTo(walkTarget, fly: false);
        QueueTeleportAfterArrival(walkTarget, destinationRow.RowId, walkSource);

        navigated = true;

        return true;
    }

    /// <summary>
    /// Queue a teleport that fires once the player reaches <paramref name="walkTarget"/>, optionally followed by a walk.
    /// </summary>
    /// <param name="walkTarget">where to walk to</param>
    /// <param name="teleportTargetRowId">aetheryte to teleport to once there</param>
    /// <param name="walkSource">the object being walked to, decides how close is considered close enough</param>
    /// <param name="finalTarget">optional point to walk to after the teleport finished</param>
    /// <param name="finalFly">whether that last walk should fly</param>
    public static void QueueTeleportAfterArrival(Vector3 walkTarget, uint teleportTargetRowId, Aetheryte? walkSource,
        Vector3? finalTarget = null, bool finalFly = false)
    {
        pendingAetheryteRowId = teleportTargetRowId;
        pendingWalkTarget = walkTarget;
        pendingStopDistance = GetStopDistance(walkSource);
        startedTerritory = Service.ClientState.TerritoryType;
        startedAt = DateTime.UtcNow;

        pendingFinalTarget = finalTarget;
        pendingFinalFly = finalFly;
        sawAreaChange = false;

        Service.Log.Information($"[Aethernet] queued walk to {walkTarget}, teleport to {teleportTargetRowId}, stopDistance={pendingStopDistance:F1}, final={(finalTarget is { } final ? final.ToString() : "none")}");
    }

    /// <summary>
    /// Walk to a point once the already issued teleport finished.
    /// </summary>
    public static void QueueFinalWalk(Vector3 target, bool fly)
    {
        pendingFinalTarget = target;
        pendingFinalFly = fly;
        teleportIssuedAt = DateTime.UtcNow;
        sawAreaChange = false;
    }

    /// <summary>
    /// A cross map teleport lands at the big aetheryte of that city. Remember the real destination so the aethernet and
    /// walking part can run after the map finished loading, the same way a chained task would do it.
    /// </summary>
    /// <param name="placeNameRowId">shard that was clicked, 0 when only a coordinate is known</param>
    /// <param name="target">coordinate to walk to after arriving</param>
    /// <param name="fly">whether that walk should fly</param>
    public static void QueueAfterTerritoryChange(uint placeNameRowId, Vector3? target = null, bool fly = false)
    {
        pendingCrossMapPlaceNameId = placeNameRowId;
        pendingCrossMapTarget = target;
        pendingCrossMapFly = fly;
        crossMapOriginTerritory = Service.ClientState.TerritoryType;
        crossMapStartedAt = DateTime.UtcNow;

        Service.Log.Information($"[Aethernet] queued cross map continuation: shard={placeNameRowId} target={target} originTerritory={crossMapOriginTerritory} fly={fly}");
    }

    /// <summary>
    /// Teleport into the territory of the target. Every territory has its own coordinate space, so the city has to be
    /// resolved through the sheet instead of by comparing world positions.
    /// </summary>
    private static bool TryTeleportToTerritory(uint mapId, Vector3 target, bool fly)
    {
        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(mapId);
        if (map.RowId is 0) return false;

        // The map marker sheet lists every aetheryte placed on that map, which is far more reliable than deriving
        // the city from the territory row, those are split across several rows per city. The big crystal of a zone
        // is preferred, a shard can only be reached through the city network.
        var gate = FindAetheryteOnMap(map, target, true) ?? FindAetheryteOnMap(map, target);

        if (gate is null)
        {
            var targetTerritory = map.TerritoryType.RowId;
            var group = FindAethernetGroupForTerritory(targetTerritory);

            // A city is entered through the big aetheryte of its group.
            gate = FindGateForGroup(group);

            // A wilderness zone has no group at all, its entry crystal is the only way in.
            if (gate is null) gate = FindGateForTerritory(targetTerritory, map.RowId);

            Service.Log.Information($"[Aethernet] no aetheryte marker on map {mapId}, falling back to territory {targetTerritory} group {group} -> gate {gate?.RowId}");
        }

        if (gate is not { } gateRow) return false;

        Service.Log.Information($"[Aethernet] cross map via aetheryte {gateRow.RowId}, then continuing to {target}");

        if (!LifestreamController.TryTeleport(gateRow.RowId, out _))
        {
            System.Teleporter.Teleport(gateRow.RowId);
        }

        QueueAfterTerritoryChange(0, target, fly);

        return true;
    }

    /// <summary>
    /// Aetheryte closest to the target among the ones the map itself declares.
    /// The map marker sheet stores the icons of a map, an aetheryte icon carries the Aetheryte row as its data key.
    /// </summary>
    /// <param name="requireBig">only the big aetheryte of a zone qualifies, a shard cannot be teleported to</param>
    private static Aetheryte? FindAetheryteOnMap(Map map, Vector3 target, bool requireBig = false)
    {
        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();

        Aetheryte? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var markerRow in Service.DataManager.GetSubrowExcelSheet<MapMarker>())
        {
            if (markerRow.RowId != map.MapMarkerRange) continue;

            foreach (var marker in markerRow)
            {
                // 60453 is the regular aetheryte icon, 60959 the alternate one used on some maps.
                if (marker.Icon is not (AetheryteIcon or AetheryteIconAlt)) continue;

                var aetheryteId = marker.DataKey.RowId;
                if (aetheryteId is 0) continue;

                var row = aetheryteSheet.GetRow(aetheryteId);
                if (row.RowId is 0) continue;

                // A shard is only reachable through the city network, the big crystal of a zone is a plain teleport.
                if (requireBig && !row.IsAetheryte) continue;

                // The map marker sheet uses plain map pixels, unlike the runtime markers which are scaled by 16.
                var worldPosition = MapPixelsToWorld(map, marker.X, marker.Y);
                var distance = Vector3.Distance(target, worldPosition);
                if (distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearest = row;
            }
        }

        if (nearest is { } found)
        {
            Service.Log.Information($"[Aethernet] map {map.RowId} declares aetheryte {found.RowId} at {nearestDistance:F0}y from the target (bigOnly={requireBig})");
        }

        return nearest;
    }

    /// <summary>
    /// Aethernet group of a territory, resolved through any aetheryte that sits there.
    /// Shards are checked as well because a city is split over several territories and the big aetheryte only
    /// belongs to one of them.
    /// </summary>
    public static uint FindAethernetGroupForTerritory(uint territory)
    {
        foreach (var candidate in Service.DataManager.GetExcelSheet<Aetheryte>())
        {
            if (candidate.AethernetGroup is 0) continue;
            if (candidate.Territory.RowId == territory) return candidate.AethernetGroup;

            foreach (var levelReference in candidate.Level)
            {
                if (levelReference.ValueNullable is not { } level) continue;
                if (level.Territory.RowId != territory) continue;

                return candidate.AethernetGroup;
            }
        }

        return 0;
    }

    /// <summary>
    /// Big aetheryte that serves as the entry point of an aethernet group.
    /// </summary>
    private static Aetheryte? FindGateForGroup(uint aethernetGroup)
    {
        // Group 0 is not a city, it is what the crystals of the wilderness zones use. Matching it would pick a
        // crystal of a completely unrelated zone.
        if (aethernetGroup is 0) return null;

        foreach (var candidate in Service.DataManager.GetExcelSheet<Aetheryte>())
        {
            if (!candidate.IsAetheryte) continue;
            if (candidate.AethernetGroup != aethernetGroup) continue;

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Big aetheryte that sits in a territory. The crystals of a wilderness zone have no aethernet group, so the
    /// group cannot resolve them and the territory is the only way to find the entry crystal of that zone.
    /// </summary>
    /// <param name="territory">territory to look in</param>
    /// <param name="mapId">map of that territory, its own crystal is preferred</param>
    private static Aetheryte? FindGateForTerritory(uint territory, uint mapId)
    {
        if (territory is 0) return null;

        Aetheryte? fallback = null;

        foreach (var candidate in Service.DataManager.GetExcelSheet<Aetheryte>())
        {
            if (!candidate.IsAetheryte) continue;
            if (!IsInTerritory(candidate, territory)) continue;

            // The crystal that belongs to the map itself is the entry of that zone.
            if (mapId is not 0 && candidate.Map.RowId == mapId) return candidate;

            fallback ??= candidate;
        }

        return fallback;
    }

    /// <summary>
    /// A big aetheryte is a much larger object, vnavmesh stops further away from it than from a small shard.
    /// </summary>
    private static float GetStopDistance(Aetheryte? source)
        => source is { IsAetheryte: true } ? AetheryteStopDistance : ShardStopDistance;

    public static void Cancel()
    {
        pendingAetheryteRowId = 0;
        pendingWalkTarget = default;
        pendingStopDistance = ShardStopDistance;
        startedTerritory = 0;
        startedAt = default;

        pendingFinalTarget = null;
        pendingFinalFly = false;
        teleportIssuedAt = default;
        lastFinalWalkLog = default;
        lastReportedInRange = false;

        pendingCrossMapPlaceNameId = 0;
        pendingCrossMapTarget = null;
        pendingCrossMapFly = false;
        crossMapOriginTerritory = 0;
        crossMapStartedAt = default;
    }

    /// <summary>
    /// Called every frame, drives the teleport chain, its final walk and the cross map continuation.
    /// </summary>
    public static void Update()
    {
        UpdateCrossMapContinuation();
        UpdateFinalWalk();

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
        var activeAetheryte = LifestreamController.GetActiveAetheryte();
        var distanceToTarget = Vector3.Distance(player.Position, pendingWalkTarget);
        var inRange = activeAetheryte is not 0 || distanceToTarget <= pendingStopDistance;

        // Log only when the state flips, printing this every frame would flood the log.
        if (inRange != lastReportedInRange)
        {
            Service.Log.Information($"[Aethernet] inRange={inRange} activeAetheryte={activeAetheryte} player={player.Position} walkTarget={pendingWalkTarget} distance={distanceToTarget:F1} stopDistance={pendingStopDistance:F1}");
            lastReportedInRange = inRange;
        }

        if (!inRange) return;

        var destination = pendingAetheryteRowId;
        var distance = Vector3.Distance(player.Position, pendingWalkTarget);

        // The walk was only a means to get in range, stop it so the player does not keep running after the teleport.
        VnavController.Stop();

        var crystal = Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(destination);

        Service.Log.Information($"[Aethernet] arrived ({distance:F1}y), teleporting to {GetAetheryteName(crystal)}");
        TryTeleportToCrystal(crystal, out _);

        pendingAetheryteRowId = 0;
        teleportIssuedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Walks to the final target once Lifestream finished moving the player.
    /// </summary>
    private static void UpdateFinalWalk()
    {
        if (pendingFinalTarget is not { } final) return;

        // Still walking towards the shard, the teleport has not been issued yet and its timestamp is meaningless.
        if (pendingAetheryteRowId is not 0) return;

        var waited = DateTime.UtcNow - teleportIssuedAt;

        // The loading screen shows up while the destination area is loaded. That is the same condition the map uses
        // to hide itself, and it is the reliable sign that the teleport really started.
        var betweenAreas = Service.Condition[ConditionFlag.BetweenAreas];

        if (betweenAreas) sawAreaChange = true;

        // Give Lifestream a moment to pick the request up before anything is issued.
        if (waited < TeleportStartupDelay) return;

        if (betweenAreas)
        {
            // The destination area is still loading.
            if (waited < AreaLoadTimeout)
            {
                LogTeleportWait("the area is still loading", waited);

                return;
            }

            Service.Log.Warning($"[Aethernet] the area is still loading after {waited.TotalSeconds:F1}s, walking anyway");
        }
        else if (sawAreaChange)
        {
            // The new area is there, the navmesh of that place needs a moment as well.
            if (!VnavController.IsReady())
            {
                if (waited < AreaLoadTimeout)
                {
                    LogTeleportWait("the navmesh of the new area is not loaded", waited);

                    return;
                }

                Service.Log.Warning($"[Aethernet] navmesh not ready after {waited.TotalSeconds:F1}s, walking anyway");
            }
        }
        else if (LifestreamController.IsBusy())
        {
            // A hop inside the city does not load a new area, Lifestream knows while it is still moving.
            if (waited < TeleportBusyTimeout)
            {
                LogTeleportWait("the teleport is still running", waited);

                return;
            }

            Service.Log.Warning($"[Aethernet] teleport still busy after {waited.TotalSeconds:F1}s, walking anyway");
        }

        pendingFinalTarget = null;

        Service.Log.Information($"[Aethernet] teleport finished, walking to {final}");
        VnavController.NavigateToWhenReady(final, pendingFinalFly, NavmeshTimeout);
    }

    /// <summary>
    /// Log the wait for the teleport, throttled so the log does not flood.
    /// </summary>
    private static void LogTeleportWait(string reason, TimeSpan waited)
    {
        if (DateTime.UtcNow - lastFinalWalkLog <= TimeSpan.FromSeconds(2.0)) return;

        lastFinalWalkLog = DateTime.UtcNow;

        Service.Log.Information($"[Aethernet] waiting for the teleport to settle: {reason} waited={waited.TotalSeconds:F1}s");
    }

    /// <summary>
    /// Runs the aethernet half of a cross map teleport once the new map has loaded.
    /// </summary>
    private static void UpdateCrossMapContinuation()
    {
        if (pendingCrossMapPlaceNameId is 0 && pendingCrossMapTarget is null) return;

        if (DateTime.UtcNow - crossMapStartedAt > CrossMapTimeout)
        {
            Service.Log.Warning("[Aethernet] the cross map continuation timed out, the teleport probably never happened");
            pendingCrossMapPlaceNameId = 0;
            pendingCrossMapTarget = null;

            return;
        }

        // Still on the loading screen, or the teleport has not started yet.
        if (Service.Condition[ConditionFlag.BetweenAreas]) return;
        if (Service.ClientState.TerritoryType == crossMapOriginTerritory) return;

        // Wait for the navmesh, otherwise the walk would be rejected right after loading.
        if (!VnavController.IsReady()) return;

        var destination = pendingCrossMapPlaceNameId;
        var target = pendingCrossMapTarget;
        var fly = pendingCrossMapFly;

        pendingCrossMapPlaceNameId = 0;
        pendingCrossMapTarget = null;

        if (target is { } coordinate)
        {
            Service.Log.Information($"[Aethernet] arrived in the new territory, continuing to {coordinate}");
            NavigateToMarker(coordinate, 0, fly);

            return;
        }

        Service.Log.Information($"[Aethernet] arrived in the new territory, continuing with aethernet to {destination}");
        Begin(destination, out _);
    }

    /// <summary>
    /// Crystal closest to an arbitrary point, used to decide where the city network drops the player off.
    /// The big aetheryte of the city belongs to the network as well, the small nodes are the stepping stones.
    /// Only crystals of the destination's own territory and aethernet group qualify. Every territory has its own
    /// coordinate space and those overlap numerically, so a plain distance search happily picks the crystal of a
    /// completely different city, and that crystal then teleports the player to that city.
    /// </summary>
    /// <param name="target">world position of the destination</param>
    /// <param name="targetTerritory">territory the destination is in, 0 when unknown</param>
    /// <param name="targetGroup">aethernet group of the destination, 0 when unknown</param>
    public static bool TryFindCrystalNearestTo(Vector3 target, uint targetTerritory, uint targetGroup, out Aetheryte? crystal, out Vector3 position, out float distance)
    {
        crystal = null;
        position = default;
        distance = float.MaxValue;

        // The group of the destination is preferred, but the crystals of a city are not always linked to the same
        // group, so the second pass takes any small node of the territory when the first one finds nothing.
        if (FindCrystal(target, targetTerritory, targetGroup, true, out crystal, out position, out distance)) return true;

        return FindCrystal(target, targetTerritory, targetGroup, false, out crystal, out position, out distance);
    }

    /// <summary>
    /// Closest crystal of the destination's territory.
    /// </summary>
    /// <param name="requireGroup">only crystals of the given aethernet group qualify</param>
    private static bool FindCrystal(Vector3 target, uint targetTerritory, uint targetGroup, bool requireGroup, out Aetheryte? crystal, out Vector3 position, out float distance)
    {
        crystal = null;
        position = default;
        distance = float.MaxValue;

        var found = false;
        var candidates = 0;
        var nearestDistance = float.MaxValue;

        foreach (var candidate in Service.DataManager.GetExcelSheet<Aetheryte>())
        {
            // The crystal has to sit in the same territory as the destination. Every territory has its own coordinate
            // space and those overlap numerically, so a plain distance search happily picks the crystal of a
            // completely different city, and the teleport would end the trip there.
            if (!IsInTerritory(candidate, targetTerritory)) continue;

            // A crystal of another group belongs to another city. The big aetheryte always belongs to its own group,
            // only the small nodes may differ in the relaxed pass.
            if (targetGroup is not 0 && candidate.AethernetGroup != targetGroup && (requireGroup || candidate.IsAetheryte)) continue;

            // Measured positions, the marker pixels are less precise and the level rows are unreliable.
            if (!AetheryteLocations.Positions.TryGetValue(candidate.RowId, out var candidatePosition)) continue;

            candidates++;

            var candidateDistance = Vector3.Distance(target, candidatePosition);
            if (candidateDistance >= nearestDistance) continue;

            nearestDistance = candidateDistance;
            crystal = candidate;
            position = candidatePosition;
            found = true;
        }

        if (found) distance = nearestDistance;

        Service.Log.Information($"[Aethernet] closest crystal to {target} in territory {targetTerritory} group {targetGroup} (groupOnly={requireGroup}): candidates={candidates} picked={crystal?.RowId} ({GetAetheryteName(crystal)}) at {position} distance={nearestDistance:F0}y");

        return found;
    }

    /// <summary>
    /// Teleport to a crystal of the city. The small nodes are used through the aethernet, the big aetheryte of the
    /// city is the paid teleport of that city and ends at the same place.
    /// </summary>
    private static bool TryTeleportToCrystal(Aetheryte crystal, out bool available)
    {
        if (!crystal.IsAetheryte) return LifestreamController.TryAethernetTeleportById(crystal.RowId, out available);

        // The big aetheryte is part of the city network, but it is also a teleport on its own. The network does not
        // accept every aetheryte, so the paid teleport of the city is the fallback.
        if (LifestreamController.TryAethernetTeleportById(crystal.RowId, out available) && available) return true;

        if (available) return false;

        if (!LifestreamController.TryTeleport(crystal.RowId, out available)) return false;

        return true;
    }

    /// <summary>
    /// Nearest point of the city the player can walk to. Positions come from the measured coordinate table, the map
    /// markers and the level rows of the sheet are not reliable enough for path finding.
    /// Only crystals of the player's own territory qualify, the player cannot walk to a crystal of another map.
    /// </summary>
    public static bool TryFindNearestSource(uint aethernetGroup, Vector3 position, out Aetheryte? source, out Vector3 sourcePosition)
    {
        source = null;
        sourcePosition = default;

        var playerTerritory = Service.ClientState.TerritoryType;

        var found = false;
        var candidates = 0;
        var nearestDistance = float.MaxValue;

        foreach (var candidate in Service.DataManager.GetExcelSheet<Aetheryte>())
        {
            if (candidate.AethernetGroup != aethernetGroup) continue;

            // The player walks to this crystal, so it has to be in the territory the player stands in. The aetherytes
            // of the displayed map are no help, the map can show a completely different part of the city.
            if (!IsInTerritory(candidate, playerTerritory)) continue;

            // Measured positions, the marker pixels are less precise and the level rows are unreliable.
            if (!AetheryteLocations.Positions.TryGetValue(candidate.RowId, out var candidatePosition)) continue;

            candidates++;

            var distance = Vector3.Distance(position, candidatePosition);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            source = candidate;
            sourcePosition = candidatePosition;
            found = true;
        }

        Service.Log.Information($"[Aethernet] nearest source in territory {playerTerritory}, group {aethernetGroup}: candidates={candidates} picked={source?.RowId} ({GetAetheryteName(source)}) at {sourcePosition} distance={nearestDistance:F0}y (origin {position})");

        return found;
    }

    /// <summary>
    /// Whether an aetheryte sits in a territory. The territory row is the authoritative one, the measured coordinates
    /// of the crystal live in that coordinate space, so a crystal of another territory cannot be walked to.
    /// </summary>
    private static bool IsInTerritory(Aetheryte aetheryte, uint territory)
    {
        if (territory is 0) return false;
        if (aetheryte.Territory.RowId == territory) return true;

        // Some rows only list the level and leave the territory empty.
        if (aetheryte.Territory.RowId is not 0) return false;

        foreach (var levelReference in aetheryte.Level) {
            if (levelReference.ValueNullable is not { } level) continue;
            if (level.Territory.RowId == territory) return true;
        }

        return false;
    }

    /// <summary>
    /// Display name of an aetheryte, a big one uses its place name and a shard its aethernet name.
    /// </summary>
    public static string GetAetheryteName(Aetheryte? aetheryte)
    {
        if (aetheryte is not { } row) return string.Empty;

        var name = row.IsAetheryte
            ? row.PlaceName.ValueNullable?.Name.ExtractText()
            : row.AethernetName.ValueNullable?.Name.ExtractText();

        if (string.IsNullOrEmpty(name)) name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? row.AethernetName.ValueNullable?.Name.ExtractText();

        return name ?? string.Empty;
    }

    /// <summary>
    /// Convert a map marker sheet position (plain map pixels, 0 to 2048) into world coordinates.
    /// Mirrors the conversion the game uses, the runtime map markers store the same value scaled by 16.
    /// </summary>
    private static Vector3 MapPixelsToWorld(Map map, float x, float y)
    {
        var scale = map.SizeFactor / 100.0f;

        return new Vector3(
            (x / scale) - map.OffsetX - (1024.0f / scale),
            0.0f,
            (y / scale) - map.OffsetY - (1024.0f / scale));
    }

    /// <summary>
    /// Position of an aetheryte as the player sees it in the world.
    /// </summary>
    public static Vector3? ResolvePosition(Aetheryte aetheryte, Vector3 playerPosition)
    {
        if (AetheryteLocations.Positions.TryGetValue(aetheryte.RowId, out var position)) return position;

        return GetClosestLevelPosition(aetheryte, playerPosition);
    }

    /// <summary>
    /// Fallback for rows the coordinate table does not cover, picks the level row closest to the player.
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
