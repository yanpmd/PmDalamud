using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Configuration;
using Lumina.Excel.Sheets;
using Mappy.Data.Crescent;
using Action = System.Action;
using Map = Lumina.Excel.Sheets.Map;

namespace Mappy.Data;

/// <summary>
/// A marker drawn on the map. Either placed by the user or part of a built in group that ships with the plugin.
/// </summary>
public class CustomMarker
{
    public uint MapId;

    /// <summary>World X</summary>
    public float X;

    /// <summary>World Z</summary>
    public float Y;

    /// <summary>World Y, only used to aim pathfinding commands, the icon is drawn from X and Z.</summary>
    public float Altitude;

    public string Name = string.Empty;
    public string GroupId = string.Empty;
    public string Note = string.Empty;
    public uint IconId = 60561;
    public float Scale = 1.0f;
    public bool FlagOnClick;
    public string Commands = string.Empty;

    /// <summary>
    /// Ships with the plugin instead of living in the user configuration, members can not be edited or deleted.
    /// </summary>
    [JsonIgnore]
    public bool BuiltIn;

    [JsonIgnore]
    public Vector2 Position => new(X, Y);

    /// <summary>
    /// Markers inside a group always use the group icon.
    /// </summary>
    [JsonIgnore]
    public uint DisplayIconId => MarkerGroups.Resolve(GroupId)?.IconId ?? IconId;

    /// <summary>
    /// Grouped markers use the size of their group, ungrouped ones use their own.
    /// </summary>
    [JsonIgnore]
    public float DisplayScale => MarkerGroups.Resolve(GroupId)?.Scale ?? Scale;

    [JsonIgnore]
    public bool IsVisible => MarkerGroups.Resolve(GroupId) is { } group
        ? System.SystemConfig.GetGroupVisibility(group.Id, group.DefaultVisible)
        : true;

    /// <summary>
    /// Icons of grouped markers are fixed to the icon of their group.
    /// </summary>
    [JsonIgnore]
    public bool IsIconLocked => MarkerGroups.Resolve(GroupId) is not null;

    /// <summary>
    /// Full 3D position, the vertical component is only an estimate and is snapped onto the navmesh before navigating.
    /// </summary>
    public Vector3 GetWorldPosition() => new(X, Altitude, Y);

    private Func<string?>? primaryTextProvider;
    private Func<string?>? secondaryTextProvider;
    private Action? clickAction;

    /// <summary>
    /// Cached delegates, building them while drawing would allocate a closure for every marker every frame.
    /// </summary>
    [JsonIgnore]
    public Func<string?> PrimaryText => primaryTextProvider ??= GetPrimaryText;

    [JsonIgnore]
    public Func<string?> SecondaryText => secondaryTextProvider ??= GetSecondaryText;

    [JsonIgnore]
    public Action ClickAction => clickAction ??= OnClicked;

    private string? GetPrimaryText()
    {
        if (Name.Length > 0) return Name;

        return MarkerGroups.Resolve(GroupId)?.DisplayName ?? string.Empty;
    }

    private string? GetSecondaryText()
    {
        var parts = new List<string>();

        // Only repeat the group name when the marker carries its own name, otherwise it is already the title.
        if (Name.Length > 0 && MarkerGroups.Resolve(GroupId) is { } group) parts.Add(group.DisplayName);
        if (Note.Length > 0) parts.Add(Note);

        return string.Join(" | ", parts);
    }

    public string GetIdString() => $"{MapId}_{X:F3}_{Y:F3}";

    public string GetMapName()
    {
        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(MapId);

        return map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
    }

    /// <summary>
    /// Distance in map coordinates (the value the game shows in the coordinate bar), not world coordinates.
    /// </summary>
    public Vector2 GetMapCoordinate() => MapUtil.WorldToMap(Position);

    public unsafe void PlaceFlag()
    {
        var agent = AgentMap.Instance();

        agent->FlagMarkerCount = 0;
        agent->SetFlagMapMarker(agent->SelectedTerritoryId, agent->SelectedMapId, X, Y);
    }

    public void ExecuteCommands()
    {
        if (Commands.Length is 0) return;

        foreach (var line in Commands.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var command = line.Trim();
            if (command.Length is 0) continue;

            try
            {
                Service.DalamudCommands.ProcessCommand(command);
            }
            catch (Exception exception)
            {
                Service.Log.Error(exception, $"Failed to run custom marker command: {command}");
            }
        }
    }

    public void OnClicked()
    {
        if (FlagOnClick) PlaceFlag();

        ExecuteCommands();
    }
}

public class CustomMarkerConfig
{
    /// <summary>
    /// Markers placed by the user, this is the only part that gets persisted.
    /// </summary>
    public List<CustomMarker> Markers = [];

    /// <summary>
    /// Groups created by the user, these are the only groups a user defined marker can join.
    /// </summary>
    public List<CustomMarkerGroup> CustomGroups = [];

    private List<CustomMarker>? builtInMarkers;

    /// <summary>
    /// Markers that ship with the plugin, generated from the migrated crescent isle catalog.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<CustomMarker> BuiltInMarkers => builtInMarkers ??= CreateBuiltInMarkers();

    public IEnumerable<CustomMarker> GetMarkersForMap(uint mapId)
    {
        foreach (var marker in BuiltInMarkers)
        {
            if (marker.MapId == mapId) yield return marker;
        }

        foreach (var marker in Markers)
        {
            if (marker.MapId == mapId) yield return marker;
        }
    }

    public IEnumerable<CustomMarker> GetVisibleMarkersForMap(uint mapId)
    {
        foreach (var marker in GetMarkersForMap(mapId))
        {
            if (marker.IsVisible) yield return marker;
        }
    }

    private static readonly HashSet<string> presentGroupIds = [];

    /// <summary>
    /// Collect every group that has at least one marker on the given map into <paramref name="buffer"/>,
    /// built in groups first, then the user created ones. The buffer is reused by the caller to stay allocation free.
    /// </summary>
    public void CollectGroupsOnMap(uint mapId, List<MarkerGroupInfo> buffer)
    {
        buffer.Clear();
        presentGroupIds.Clear();

        foreach (var marker in GetMarkersForMap(mapId))
        {
            if (marker.GroupId.Length > 0) presentGroupIds.Add(marker.GroupId);
        }

        if (presentGroupIds.Count is 0) return;

        foreach (var group in MarkerGroups.All)
        {
            if (!presentGroupIds.Contains(group.Id)) continue;
            if (MarkerGroups.Resolve(group.Id) is not { } info) continue;

            buffer.Add(info);
        }

        foreach (var group in CustomGroups)
        {
            if (!presentGroupIds.Contains(group.Id)) continue;
            if (MarkerGroups.Resolve(group.Id) is not { } info) continue;

            buffer.Add(info);
        }
    }

    public CustomMarkerGroup AddCustomGroup()
    {
        var group = new CustomMarkerGroup
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = Localization.Get("CustomGroup.DefaultName", "New Group"),
            IconId = 60561,
        };

        CustomGroups.Add(group);
        Save();

        return group;
    }

    public void RemoveCustomGroup(CustomMarkerGroup group)
    {
        CustomGroups.Remove(group);

        // Markers of a deleted group fall back to being ungrouped.
        foreach (var marker in Markers)
        {
            if (marker.GroupId == group.Id) marker.GroupId = string.Empty;
        }

        System.SystemConfig.MarkerGroupVisibility.Remove(group.Id);
        SystemConfig.Save();

        Save();
    }

    public static CustomMarkerConfig Load() => Service.PluginInterface.LoadConfigFile<CustomMarkerConfig>("CustomMarkers.data.json");

    public void Save()
    {
        // Groups may have been renamed, re-iconed or resized, the resolved copies are stale now.
        MarkerGroups.InvalidateCache();

        Service.PluginInterface.SaveConfigFile("CustomMarkers.data.json", System.CustomMarkerConfig);
    }

    private static List<CustomMarker> CreateBuiltInMarkers()
    {
        var markers = new List<CustomMarker>();
        var treasureSheet = Service.DataManager.GetExcelSheet<Treasure>();

        foreach (var north in new[] { false, true })
        {
            foreach (var chest in CrescentChestCatalog.Get(north))
            {
                var treasureRowId = treasureSheet.GetRow((uint)chest.TreasureRowId).SGB.RowId;
                var groupId = treasureRowId switch
                {
                    CrescentChestCatalog.BronzeTreasureRowId => MarkerGroups.BronzeChest,
                    CrescentChestCatalog.SilverTreasureRowId => MarkerGroups.SilverChest,
                    _ => string.Empty,
                };

                if (groupId.Length is 0) continue;

                markers.Add(CreateBuiltIn(CrescentChestCatalog.GetMapId(north, chest.TreasureRowId), chest.Position, groupId));
            }

            var potNorth = north ? CrescentPointCatalog.NorthPotNorth : CrescentPointCatalog.SouthPotNorth;
            var potSouth = north ? CrescentPointCatalog.NorthPotSouth : CrescentPointCatalog.SouthPotSouth;
            var rerolls = north ? CrescentPointCatalog.NorthRerolls : CrescentPointCatalog.SouthRerolls;
            var carrots = north ? CrescentPointCatalog.NorthCarrots : CrescentPointCatalog.SouthCarrots;
            var surveys = north ? CrescentPointCatalog.NorthSurveyPoints : CrescentPointCatalog.SouthSurveyPoints;
            var mapId = north ? CrescentChestCatalog.NorthMapId : CrescentChestCatalog.SouthMapId;

            foreach (var position in potNorth) markers.Add(CreateBuiltIn(mapId, position, MarkerGroups.MagicPot));
            foreach (var position in potSouth) markers.Add(CreateBuiltIn(mapId, position, MarkerGroups.MagicPot));
            foreach (var position in rerolls) markers.Add(CreateBuiltIn(mapId, position, MarkerGroups.RerollChest));
            foreach (var position in carrots) markers.Add(CreateBuiltIn(mapId, position, MarkerGroups.Carrot));

            foreach (var survey in surveys)
            {
                markers.Add(CreateBuiltIn(survey.MapId, survey.Position, MarkerGroups.Survey));
            }
        }

        Service.Log.Debug($"[CustomMarkers] Generated {markers.Count} built in markers.");

        return markers;
    }

    private static CustomMarker CreateBuiltIn(uint mapId, Vector3 position, string groupId) => new()
    {
        MapId = mapId,
        X = position.X,
        Y = position.Z,
        Altitude = position.Y,
        GroupId = groupId,
        BuiltIn = true,
        IconId = MarkerGroups.Get(groupId)?.IconId ?? 0,
    };
}
