using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Extensions;
using Lumina.Excel.Sheets;
using Mappy.Classes;
using Mappy.Controllers;
using Map = Lumina.Excel.Sheets.Map;
using MarkerInfo = Mappy.Classes.MarkerInfo;

namespace Mappy.Extensions;

public static class MapMarkerInfoExtensions {
    public static void Draw(this MapMarkerInfo marker, Vector2 offset, float scale) {
        var tooltipText = marker.MapMarker.Subtext.AsDalamudSeString();
        var popupSeed = MarkerContextMenu.GetPopupSeed(marker.MapMarker.IconId, marker.MapMarker.X, marker.MapMarker.Y);

        var markerInfo = new MarkerInfo {
            // Divide by 16, as it seems they use a fixed scalar
            // Add 1024 * scale, to offset from top-left, to center-based coordinate
            // Add offset for drawing relative to map when it's moved around
            Position = new Vector2(marker.MapMarker.X, marker.MapMarker.Y) / 16.0f * scale +
                       DrawHelpers.GetMapCenterOffsetVector() * scale,
            Offset = offset,
            Scale = scale,
            Radius = marker.MapMarker.Scale,
            RadiusColor = KnownColor.MediumPurple.Vector(),
            IconId = marker.MapMarker.IconId,
            PrimaryText = GetMarkerPrimaryTooltip(marker, tooltipText),
            OnLeftClicked = () => OnMarkerClicked(ref marker),
            PopupSeed = popupSeed,
            SecondaryText = () => GetTooltip(ref marker),
        };

        if (marker.MapMarker.IconId is 0 && marker.MapMarker.Index is not 0) {
            TryDrawText(marker, markerInfo, tooltipText);
        }
        else {
            DrawHelpers.DrawMapMarker(markerInfo);
            MarkerContextMenu.Draw(popupSeed, null, markerInfo.Position + markerInfo.Offset, null);
        }
    }

    private static void TryDrawText(MapMarkerInfo marker, MarkerInfo markerInfo, SeString tooltipText) {
        if (!System.SystemConfig.ShowTextLabels) return;

        var textTypeScalar = marker.MapMarker.SubtextStyle switch {
            1 => System.SystemConfig.LargeAreaTextScale,
            _ => System.SystemConfig.SmallAreaTextScale,
        };

        if (System.SystemConfig.ScaleTextWithZoom) {
            markerInfo.Scale *= textTypeScalar * 0.33f;
        }
        else {
            markerInfo.Scale = textTypeScalar * 0.33f;
        }

        DrawHelpers.DrawText(markerInfo, tooltipText);
    }

    private static void OnMarkerClicked(ref MapMarkerInfo marker) {
        switch (marker.DataType) {
            case 1: // MapLinkMarker
                OnMapLinkMarkerClicked(ref marker);
                break;

            case 2: // InstanceLink
                OnInstanceLinkClicked(ref marker);
                break;

            case 3: // Aetheryte
                OnAetheryteClicked(ref marker);
                break;

            case 4: // Aethernet
                OnAethernetClicked(ref marker);
                break;
        }
    }

    private static void OnMapLinkMarkerClicked(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0) return;
        if (DrawHelpers.IsDisallowedIcon(marker.MapMarker.IconId)) return;

        System.IntegrationsController.OpenMap(marker.DataKey);
    }

    private static void OnInstanceLinkClicked(ref MapMarkerInfo _) {
        // Might consider opening contents finder to this duty, maybe
    }

    private static void OnAetheryteClicked(ref MapMarkerInfo marker) {
        var dataKey = marker.DataKey;
        if (dataKey is 0) return;

        // Teleporting is not possible while in combat, just ignore the click.
        if (Service.Condition[ConditionFlag.InCombat]) return;

        // A direct teleport replaces whatever shard walk was still pending.
        AethernetNavigator.Cancel();

        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();
        var aetheryte = aetheryteSheet.GetRow(dataKey);

        // A city spans several territories, so clicking the big aetheryte of the other layer must not trigger a
        // cross map teleport. Handle it exactly like a shard click instead.
        if (aetheryte.RowId is not 0 && IsInSameArea(aetheryte) && Service.ObjectTable.LocalPlayer is { } player) {
            // A big aetheryte is a valid aethernet destination on its own, no need to aim at a neighbouring shard.
            if (LifestreamController.GetActiveAetheryte() is not 0) {
                var teleportStarted = LifestreamController.TryAethernetTeleportById(dataKey, out var lifestreamAvailable);

                Service.Log.Information($"[Aethernet] in range, aetheryte {dataKey} available={lifestreamAvailable} started={teleportStarted}");

                if (lifestreamAvailable) return;
            }

            // Out of range: walk to the closest usable point of this city and teleport to the aetheryte from there.
            if (AethernetNavigator.TryFindNearestSource(aetheryte.AethernetGroup, player.Position, out var source, out var sourcePosition)) {
                if (source is { } sourceRow && sourceRow.RowId == dataKey) {
                    // The closest point already is this aetheryte, simply walk over.
                    Service.Log.Information($"[Aethernet] walking to aetheryte {dataKey} at {sourcePosition}");
                    VnavController.NavigateTo(sourcePosition, fly: false);

                    return;
                }

                Service.Log.Information($"[Aethernet] out of range, walking to source {source?.RowId} at {sourcePosition} for aetheryte {dataKey}");
                VnavController.NavigateTo(sourcePosition, fly: false);
                AethernetNavigator.QueueTeleportAfterArrival(sourcePosition, dataKey);

                return;
            }

            // No usable shard, just walk to the aetheryte itself.
            if (AethernetNavigator.ResolvePosition(aetheryte, player.Position) is { } walkTarget) {
                Service.Log.Information($"[Aethernet] walking to aetheryte {dataKey} at {walkTarget}");
                VnavController.NavigateTo(walkTarget, fly: false);

                return;
            }
        }

        Service.Log.Information($"[Aethernet] teleporting to aetheryte {dataKey}");

        // Let Lifestream handle it when available, it deals with server changes and queues.
        if (LifestreamController.TryTeleport(dataKey, out _)) return;

        System.Teleporter.Teleport(dataKey);
    }

    private static void OnAethernetClicked(ref MapMarkerInfo marker) {
        var dataKey = marker.DataKey;
        if (dataKey is 0) return;

        // Teleporting is not possible while in combat, just ignore the click.
        if (Service.Condition[ConditionFlag.InCombat]) return;

        var masterAetheryte = GetAetheryteForAethernet(dataKey);
        if (masterAetheryte is not { } aetheryte) return;

        var shard = Service.DataManager.GetExcelSheet<Aetheryte>()
            .FirstOrNull(entry => entry.AethernetName.RowId == dataKey);

        // Already in that area, so the player wants to hop between the shards of this city instead of leaving the map.
        if (shard is { } shardRow && IsInSameArea(shardRow)) {
            // Lifestream accepts the request even when the player is out of range and only fails later inside its own
            // task, so the range check has to happen here before anything is queued.
            if (LifestreamController.GetActiveAetheryte() is not 0) {
                var teleportStarted = LifestreamController.TryAethernetTeleportById(shardRow.RowId, out var lifestreamAvailable);

                Service.Log.Information($"[Aethernet] in range, dataKey={dataKey} shard={shardRow.RowId} available={lifestreamAvailable} started={teleportStarted}");

                // Installed and accepted, or installed but refused (busy), nothing sensible to do in either case.
                if (lifestreamAvailable) return;
            }

            Service.Log.Information($"[Aethernet] out of range, walking to nearest shard for dataKey={dataKey}");

            AethernetNavigator.Begin(dataKey, out var navigated);
            if (navigated) return;

            // No reachable shard on this floor, the shard belongs to another one, so fall back to a regular teleport.
            Service.Log.Information($"[Aethernet] no reachable shard on this floor, teleporting to aetheryte {aetheryte.RowId}");
        }

        Service.Log.Information($"[Aethernet] regular teleport to aetheryte {aetheryte.RowId} (dataKey={dataKey})");

        if (aetheryte.RowId is 0) return;

        if (LifestreamController.TryTeleport(aetheryte.RowId, out _)) return;

        System.Teleporter.Teleport(aetheryte.RowId);
    }

    private static string GetTooltip(ref MapMarkerInfo marker) {
        switch (marker.DataType) {
            case 1: // MapLinkMarker
                return GetMapLinkTooltip(ref marker);

            case 2: // InstanceLink
                return GetInstanceLinkTooltip(ref marker);

            case 3: // Aetheryte
                return GetAetheryteTooltip(ref marker);

            case 4: // Aethernet
                return GetAethernetTooltip(ref marker);
        }

        return string.Empty;
    }

    private static string GetMapLinkTooltip(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0) return string.Empty;
        if (DrawHelpers.IsDisallowedIcon(marker.MapMarker.IconId)) return string.Empty;

        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(marker.DataKey);
        var mapPlaceName = map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;

        return Localization.F("Marker.OpenMap", mapPlaceName);
    }

    private static string GetInstanceLinkTooltip(ref MapMarkerInfo marker) {
        return Localization.F("Marker.InstanceLink", marker.DataKey);
    }

    private static string GetAetheryteTooltip(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0) return string.Empty;

        var aetheryteTeleportCost = GetAetheryteTeleportGilCost(marker.DataKey);
        if (aetheryteTeleportCost is null) return Localization.Get("Marker.NotAttuned", "Not attuned to aetheryte");

        var aetheryte = Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(marker.DataKey);
        var aetherytePlaceName = aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var aetheryteCost = GetAetheryteTeleportCost(marker.DataKey);

        return Localization.F("Marker.Teleport", aetherytePlaceName, aetheryteCost);
    }

    private static string GetAethernetTooltip(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0) return string.Empty;

        var aetheryte = GetAetheryteForAethernet(marker.DataKey);
        if (aetheryte is null) return string.Empty;
        if (aetheryte.Value.RowId is 0) return string.Empty;

        var aetherytePlaceName = aetheryte.Value.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;

        return Localization.F("Marker.Teleport", aetherytePlaceName, GetAetheryteTeleportCost(aetheryte.Value.RowId));
    }

    /// <summary>
    /// Whether the aetheryte belongs to the area the player is currently in.
    /// A city spans several territories and maps (Limsa Lower and Upper Decks are 128 and 129), and the sheet data for
    /// those does not line up consistently, so several independent checks are used from most to least precise.
    /// </summary>
    private static unsafe bool IsInSameArea(Aetheryte target) {
        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();
        var currentTerritory = Service.ClientState.TerritoryType;

        // 0) The entry itself sits in the area the player is in. The master aetheryte of a city usually belongs to a
        // different layer (Limsa Upper vs Lower Decks), so it has to be checked on its own.
        if (target.Territory.RowId == currentTerritory) {
            Service.Log.Information($"[Aethernet] area=shard-territory target={target.Territory.RowId} current={currentTerritory}");

            return true;
        }

        foreach (var levelReference in target.Level) {
            if (levelReference.ValueNullable is not { } level) continue;
            if (level.Territory.RowId != currentTerritory) continue;

            Service.Log.Information($"[Aethernet] area=shard-level-territory target={level.Territory.RowId} current={currentTerritory}");

            return true;
        }

        // 1) The player stands next to an aetheryte, the shared group is the exact same-city marker.
        var activeAetheryteId = LifestreamController.GetActiveAetheryte();
        if (activeAetheryteId is not 0) {
            var activeAetheryte = aetheryteSheet.GetRow(activeAetheryteId);
            if (activeAetheryte.RowId is not 0) {
                Service.Log.Information($"[Aethernet] area=group active={activeAetheryteId} group={activeAetheryte.AethernetGroup} target={target.AethernetGroup}");

                return activeAetheryte.AethernetGroup == target.AethernetGroup;
            }
        }

        // 2) Compare the map the entry sits on with the map the player is on, through the city name both share.
        // AgentMap.CurrentMapId is an index, not a Map row, so the client state value has to be used.
        var mapSheet = Service.DataManager.GetExcelSheet<Map>();
        var currentMapId = Service.ClientState.MapId;
        var currentPlaceName = mapSheet.GetRow(currentMapId).PlaceName.RowId;

        if (currentPlaceName is not 0) {
            foreach (var levelReference in target.Level) {
                if (levelReference.ValueNullable is not { } level) continue;
                if (level.Map.RowId is 0) continue;
                if (mapSheet.GetRow(level.Map.RowId).PlaceName.RowId != currentPlaceName) continue;

                Service.Log.Information($"[Aethernet] area=map shardMap={level.Map.RowId} currentMap={currentMapId} place={currentPlaceName}");

                return true;
            }
        }

        // Distance is deliberately not used here, every territory has its own coordinate space and two different
        // cities can be numerically close, which made a cross city click walk instead of teleport.
        Service.Log.Information($"[Aethernet] area=none currentMap={currentMapId} place={currentPlaceName} territory={target.Territory.RowId} current={currentTerritory}");

        return false;
    }

    private static Aetheryte? GetAetheryteForAethernet(uint aethernetKey) =>
        System.AetheryteAethernetCache.GetValue(aethernetKey);

    private static uint? GetAetheryteTeleportGilCost(uint aethernetKey) => Service.AetheryteList
        .FirstOrDefault(entry => entry.AetheryteId == aethernetKey)?.GilCost;

    private static string GetAetheryteTeleportCost(uint targetDataKey) =>
        $"({GetAetheryteTeleportGilCost(targetDataKey) ?? 0:n0} {SeIconChar.Gil.ToIconChar()})";

    private static Func<string> GetMarkerPrimaryTooltip(MapMarkerInfo marker, SeString tooltipText)
    {
        if (DrawHelpers.IsDisallowedIcon(marker.MapMarker.IconId)) return () => string.Empty;
        if (!System.SystemConfig.ShowMiscTooltips) return () => string.Empty;
        if (!tooltipText.TextValue.IsNullOrEmpty()) return tooltipText.ToString;

        return marker.DataType switch
        {
            4 => () => Service.DataManager.GetExcelSheet<PlaceName>().GetRow(marker.DataKey).Name.ExtractText(),
            _ => () => System.TooltipCache.GetValue(marker.MapMarker.IconId) ?? string.Empty,
        };
    }

}