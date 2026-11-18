using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Configuration;
using Lumina.Excel.Sheets;
using Map = Lumina.Excel.Sheets.Map;

namespace Mappy.Data;

/// <summary>
/// A location the player saved for later. Coordinates are stored in world space, the same way custom markers are,
/// so a favorite can be navigated to with the regular path finding code.
/// </summary>
public class FavoritePoint
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string GroupId = string.Empty;
    public uint IconId = 60561;
    public string Note = string.Empty;

    public uint MapId;

    /// <summary>World X</summary>
    public float X;

    /// <summary>World Z</summary>
    public float Y;

    /// <summary>World Y</summary>
    public float Altitude;

    [JsonIgnore]
    public Vector2 Position => new(X, Y);

    [JsonIgnore]
    public Vector3 WorldPosition => new(X, Altitude, Y);

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? GetMapName() : Name;

    public string GetMapName()
    {
        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(MapId);

        return map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
    }

    /// <summary>
    /// Distance in map coordinates (the value the game shows in the coordinate bar), not world coordinates.
    /// </summary>
    public Vector2 GetMapCoordinate() => MapUtil.WorldToMap(Position);

    /// <summary>
    /// Capture where the player actually stands, even when a different map is being viewed right now.
    /// </summary>
    public static unsafe FavoritePoint CaptureCurrentLocation()
    {
        var playerPosition = Service.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
        var mapId = AgentMap.Instance()->CurrentMapId;
        if (mapId is 0) mapId = AgentMap.Instance()->SelectedMapId;

        var point = new FavoritePoint
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            MapId = mapId,
            X = playerPosition.X,
            Y = playerPosition.Z,
            Altitude = playerPosition.Y,
        };

        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(mapId);
        var mapName = map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var coordinate = point.GetMapCoordinate();
        point.Name = string.IsNullOrEmpty(mapName)
            ? $"{coordinate.X:F1}, {coordinate.Y:F1}"
            : $"{mapName} ({coordinate.X:F1}, {coordinate.Y:F1})";

        return point;
    }
}

/// <summary>
/// A user created group used to arrange favorites in the sidebar.
/// </summary>
public class FavoriteGroup
{
    public string Id = string.Empty;
    public string Name = string.Empty;
}

/// <summary>
/// Persisted favorites and their groups. Stored separately from custom markers so the two features stay independent.
/// </summary>
public class FavoriteConfig
{
    public List<FavoritePoint> Favorites = [];
    public List<FavoriteGroup> Groups = [];

    [JsonIgnore]
    public IEnumerable<IGrouping<string, FavoritePoint>> GroupedFavorites => Favorites
        .GroupBy(point => point.GroupId ?? string.Empty)
        .OrderBy(group => group.Key.Length is 0 ? 0 : 1)
        .ThenBy(group => GetGroupName(group.Key), StringComparer.CurrentCultureIgnoreCase);

    public string GetGroupName(string groupId)
    {
        if (string.IsNullOrEmpty(groupId)) return Localization.Get("FavoriteSidebar.Ungrouped", "Ungrouped");

        foreach (var group in Groups) {
            if (group.Id == groupId) return group.Name;
        }

        return Localization.Get("FavoriteSidebar.Ungrouped", "Ungrouped");
    }

    public FavoriteGroup AddGroup(string name)
    {
        var group = new FavoriteGroup
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = string.IsNullOrWhiteSpace(name)
                ? Localization.Get("FavoriteEditor.NewGroup", "New Group")
                : name.Trim(),
        };

        Groups.Add(group);
        Save();

        return group;
    }

    public void RemoveGroup(string groupId)
    {
        Groups.RemoveAll(group => group.Id == groupId);

        foreach (var point in Favorites) {
            if (point.GroupId == groupId) point.GroupId = string.Empty;
        }

        Save();
    }

    public void Add(FavoritePoint point)
    {
        Favorites.Add(point);
        Save();
    }

    public void Remove(FavoritePoint point)
    {
        Favorites.Remove(point);
        Save();
    }

    public static FavoriteConfig Load()
        => Service.PluginInterface.LoadConfigFile<FavoriteConfig>("Favorites.data.json");

    public void Save()
        => Service.PluginInterface.SaveConfigFile("Favorites.data.json", System.FavoriteConfig);
}
