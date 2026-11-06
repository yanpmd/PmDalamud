using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Aetheryte = Lumina.Excel.Sheets.Aetheryte;

namespace Mappy.Classes;

/// <summary>
/// Region -> map -> area -> sub area hierarchy of every map the game lists, plus a flat index for searching.
/// Shared by the map browser sidebar and the map search overlay, the excel sheets never change while the game runs.
/// </summary>
public static class MapIndex
{
    /// <summary>
    /// A named place inside a map, an aetheryte or a marker with a name.
    /// </summary>
    public sealed class SubArea
    {
        public required string Label { get; init; }
        public required uint IconId { get; init; }
        public required int SortOrder { get; init; }

        /// <summary>
        /// Position inside the map in texture pixels, null when the place has no known position.
        /// </summary>
        public Vector2? Position { get; init; }
    }

    /// <summary>
    /// One map of a zone, cities are split into several of these.
    /// </summary>
    public sealed class Area
    {
        public required Map Map { get; init; }

        /// <summary>
        /// Expansion the map was added in, 0 is A Realm Reborn.
        /// </summary>
        public required uint Version { get; init; }

        /// <summary>
        /// Territory of the map, the game assigns these in release order within an expansion.
        /// </summary>
        public required uint TerritoryRowId { get; init; }

        /// <summary>
        /// Whether an aetheryte of the map can be used to teleport there.
        /// </summary>
        public bool HasAetheryte { get; init; }

        /// <summary>
        /// Whether the map belongs to instanced content, a dungeon, a trial or anything else that is entered
        /// through a duty.
        /// </summary>
        public bool IsInstance { get; init; }

        public string Label { get; set; } = string.Empty;
        public List<SubArea> SubAreas { get; } = [];
    }

    public sealed class Zone
    {
        public required string Name { get; init; }
        public List<Area> Areas { get; } = [];
    }

    public sealed class Region
    {
        public required string Name { get; init; }
        public List<Zone> Zones { get; } = [];
    }

    /// <summary>
    /// One hit of the search, <paramref name="Context"/> is the short hierarchy hint shown next to the label.
    /// </summary>
    public sealed record SearchEntry(string Label, string Context, string Path, uint MapId, uint IconId, Vector2? Position);

    private static readonly List<Region> RegionList = [];
    private static readonly List<(SearchEntry Entry, string Haystack)> SearchEntries = [];
    private static bool isBuilt;

    public static IReadOnlyList<Region> Regions
    {
        get
        {
            EnsureBuilt();

            return RegionList;
        }
    }

    public static void EnsureBuilt()
    {
        if (isBuilt) return;

        isBuilt = true;

        Build();
    }

    /// <summary>
    /// Fills <paramref name="results"/> with everything matching the query, in release order.
    /// </summary>
    public static void Search(string query, List<SearchEntry> results)
    {
        results.Clear();

        if (string.IsNullOrWhiteSpace(query)) return;

        EnsureBuilt();

        foreach (var (entry, haystack) in SearchEntries) {
            if (!haystack.Contains(query, StringComparison.CurrentCultureIgnoreCase)) continue;

            results.Add(entry);
        }
    }

    private static void Build()
    {
        // The aetherytes are the crystals the player can teleport to, a map without one is a dungeon or a place the
        // crystal network does not reach.
        var aetheryteMapIds = new HashSet<uint>();
        var aetheryteTerritoryIds = new HashSet<uint>();

        foreach (var aetheryte in Service.DataManager.GetExcelSheet<Aetheryte>()) {
            if (aetheryte is not { IsAetheryte: true }) continue;

            if (aetheryte.Map.RowId is not 0) aetheryteMapIds.Add(aetheryte.Map.RowId);
            if (aetheryte.Territory.RowId is not 0) aetheryteTerritoryIds.Add(aetheryte.Territory.RowId);
        }

        var maps = Service.DataManager.GetExcelSheet<Map>()
            .Where(map => map is { PlaceName.RowId: not 0, TerritoryType.ValueNullable.LoadingImage.RowId: not 0, })
            .Where(map => map is not { PriorityUI: 0, PriorityCategoryUI: 0 })
            .ToList();

        var areaByMapId = new Dictionary<uint, Area>();
        var areaByMarkerRange = new Dictionary<uint, Area>();
        var regionByName = new Dictionary<string, Region>(StringComparer.OrdinalIgnoreCase);

        foreach (var map in maps) {
            var regionName = GetPlaceName(map.PlaceNameRegion);
            if (regionName.Length is 0) regionName = Localization.Get("MapSelection.Other", "Other");

            if (!regionByName.TryGetValue(regionName, out var region)) {
                region = new Region { Name = regionName };
                regionByName.Add(regionName, region);
                RegionList.Add(region);
            }

            var zoneName = GetPlaceName(map.PlaceName);
            var zone = region.Zones.FirstOrDefault(existing => string.Equals(existing.Name, zoneName, StringComparison.Ordinal));
            if (zone is null) {
                zone = new Zone { Name = zoneName };
                region.Zones.Add(zone);
            }

            var territory = map.TerritoryType.ValueNullable;

            var area = new Area {
                Map = map,
                Version = territory?.ExVersion.RowId ?? uint.MaxValue,
                TerritoryRowId = map.TerritoryType.RowId,
                HasAetheryte = HasAetheryte(map, territory, aetheryteMapIds, aetheryteTerritoryIds),
                IsInstance = territory?.ContentFinderCondition.RowId is not null and not 0,
            };

            zone.Areas.Add(area);
            areaByMapId[map.RowId] = area;

            if (map.MapMarkerRange is not 0) areaByMarkerRange[(uint)map.MapMarkerRange] = area;
        }

        // Everything is listed in release order, region and zone take the first map they contain as their key.
        foreach (var region in RegionList) {
            foreach (var zone in region.Zones) {
                zone.Areas.Sort(CompareAreas);

                foreach (var area in zone.Areas) area.Label = GetAreaLabel(area.Map, zone.Name, zone.Areas.Count > 1);
            }

            region.Zones.Sort((left, right) => CompareAreas(left.Areas[0], right.Areas[0]));
        }

        RegionList.Sort((left, right) => CompareAreas(left.Zones[0].Areas[0], right.Zones[0].Areas[0]));

        AddAetherytes(areaByMapId);
        AddPlacesOfInterest(areaByMarkerRange);

        foreach (var region in RegionList) {
            foreach (var zone in region.Zones) {
                foreach (var area in zone.Areas) {
                    area.SubAreas.Sort((left, right) => left.SortOrder != right.SortOrder
                        ? left.SortOrder.CompareTo(right.SortOrder)
                        : string.Compare(left.Label, right.Label, StringComparison.CurrentCulture));

                    var path = $"{region.Name} / {zone.Name} / {area.Label}";
                    var mapLabel = area.Label;

                    SearchEntries.Add((new SearchEntry(mapLabel, region.Name, path, area.Map.RowId, 0, null), $"{path} {mapLabel}"));

                    var subAreaContext = string.Equals(area.Label, zone.Name, StringComparison.Ordinal) ? region.Name : $"{region.Name} / {area.Label}";

                    foreach (var subArea in area.SubAreas) {
                        SearchEntries.Add((new SearchEntry(subArea.Label, subAreaContext, $"{path} / {subArea.Label}", area.Map.RowId, subArea.IconId, subArea.Position),
                            $"{path} / {subArea.Label}"));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Whether the player can teleport to the map with an aetheryte.
    /// </summary>
    private static bool HasAetheryte(Map map, TerritoryType? territory, HashSet<uint> aetheryteMapIds, HashSet<uint> aetheryteTerritoryIds)
    {
        // The territory points at the crystal of the zone, that is the most direct answer.
        if (territory?.Aetheryte.RowId is not null and not 0) return true;

        // The crystal can also sit on a map of the territory, the sheet does not always fill both fields.
        if (aetheryteMapIds.Contains(map.RowId)) return true;

        return aetheryteTerritoryIds.Contains(map.TerritoryType.RowId);
    }

    private static void AddAetherytes(Dictionary<uint, Area> areaByMapId)
    {
        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();

        // Aethernet shards do not carry a map, they have to borrow the map of their group's aetheryte.
        var mapIdByGroup = new Dictionary<uint, uint>();
        foreach (var aetheryte in aetheryteSheet) {
            if (aetheryte is { IsAetheryte: true, AethernetGroup: not 0, Map.RowId: not 0 }) {
                mapIdByGroup[aetheryte.AethernetGroup] = aetheryte.Map.RowId;
            }
        }

        foreach (var aetheryte in aetheryteSheet) {
            if (aetheryte is { PlaceName.RowId: 0, AethernetName.RowId: 0, }) continue;

            var mapId = aetheryte.Map.RowId;
            if (mapId is 0 && !mapIdByGroup.TryGetValue(aetheryte.AethernetGroup, out mapId)) continue;
            if (!areaByMapId.TryGetValue(mapId, out var area)) continue;

            var name = aetheryte.AethernetName.RowId is not 0 ? GetPlaceName(aetheryte.AethernetName) : GetPlaceName(aetheryte.PlaceName);
            if (name.Length is 0) continue;

            area.SubAreas.Add(new SubArea {
                Label = name,
                IconId = aetheryte.IsAetheryte ? 60453u : 60430u,
                SortOrder = 0,
            });
        }
    }

    private static void AddPlacesOfInterest(Dictionary<uint, Area> areaByMarkerRange)
    {
        foreach (var marker in Service.DataManager.GetSubrowExcelSheet<MapMarker>().SelectMany(collection => collection)) {
            if (marker is { PlaceNameSubtext.RowId: 0 } or { Icon: not 60442 }) continue;
            if (!areaByMarkerRange.TryGetValue(marker.RowId, out var area)) continue;

            var name = GetPlaceName(marker.PlaceNameSubtext);
            if (name.Length is 0) continue;

            area.SubAreas.Add(new SubArea {
                Label = name,
                IconId = 60442,
                SortOrder = 1,

                // Excel marker positions are map texture pixels, the agent scales them by 16 instead.
                Position = new Vector2(marker.X, marker.Y),
            });
        }
    }

    /// <summary>
    /// Release order: expansion first, then the order the game added the territory and its layers in.
    /// </summary>
    private static int CompareAreas(Area left, Area right)
    {
        var result = left.Version.CompareTo(right.Version);
        if (result is not 0) return result;

        result = left.TerritoryRowId.CompareTo(right.TerritoryRowId);
        if (result is not 0) return result;

        result = left.Map.MapIndex.CompareTo(right.Map.MapIndex);

        return result is not 0 ? result : left.Map.RowId.CompareTo(right.Map.RowId);
    }

    private static string GetPlaceName(RowRef<PlaceName> reference)
        => reference.ValueNullable?.Name.ExtractText() ?? string.Empty;

    /// <summary>
    /// A zone with a single map is simply called like the zone. Cities are split into several maps that share
    /// a name, there the sub name (or the layer index) has to be added so the entries stay distinguishable.
    /// </summary>
    private static string GetAreaLabel(Map map, string zoneName, bool multipleAreas)
    {
        if (!multipleAreas) return zoneName;

        var subName = GetPlaceName(map.PlaceNameSub);
        if (subName.Length is not 0 && !string.Equals(subName, zoneName, StringComparison.Ordinal)) return $"{zoneName} {subName}";

        return $"{zoneName} {Localization.F("MapSelection.Layer", map.MapIndex)}";
    }
}
