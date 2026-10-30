using System.Collections.Generic;

namespace Mappy.Data;

/// <summary>
/// A group that ships with the plugin. Only used by the built in marker catalog, users can not put their own
/// markers into these because their content is fixed.
/// </summary>
public sealed class MarkerGroup
{
    public required string Id { get; init; }
    public required uint IconId { get; init; }
    public required string NameKey { get; init; }
    public bool DefaultVisible { get; init; }
    public float Scale { get; init; } = 1.0f;
}

/// <summary>
/// A group created by the user, this is the only kind of group a user defined marker can join.
/// </summary>
public class CustomMarkerGroup
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public uint IconId = 60561;
    public float Scale = 1.0f;
}

/// <summary>
/// Resolved view of a group, independent of whether it is built in or user defined.
/// </summary>
public readonly record struct MarkerGroupInfo(string Id, uint IconId, string DisplayName, bool DefaultVisible, bool BuiltIn, float Scale);

public static class MarkerGroups
{
    public const string BronzeChest = "BronzeChest";
    public const string SilverChest = "SilverChest";
    public const string MagicPot = "MagicPot";
    public const string RerollChest = "RerollChest";
    public const string Carrot = "Carrot";
    public const string Survey = "Survey";

    public static readonly MarkerGroup[] All =
    [
        new() { Id = BronzeChest, IconId = 60356, NameKey = "MarkerGroup.BronzeChest", DefaultVisible = true, },
        new() { Id = SilverChest, IconId = 60355, NameKey = "MarkerGroup.SilverChest", DefaultVisible = true, },
        new() { Id = MagicPot, IconId = 60354, NameKey = "MarkerGroup.MagicPot", DefaultVisible = false, },
        new() { Id = RerollChest, IconId = 61473, NameKey = "MarkerGroup.RerollChest", DefaultVisible = false, },
        new() { Id = Carrot, IconId = 25207, NameKey = "MarkerGroup.Carrot", DefaultVisible = false, },
        new() { Id = Survey, IconId = 60468, NameKey = "MarkerGroup.Survey", DefaultVisible = false, },
    ];

    /// <summary>
    /// Look up a built in group, user defined groups are not resolved here.
    /// </summary>
    public static MarkerGroup? Get(string? groupId)
    {
        if (string.IsNullOrEmpty(groupId)) return null;

        foreach (var group in All)
        {
            if (group.Id == groupId) return group;
        }

        return null;
    }

    /// <summary>
    /// Resolution is done per marker per frame, so the result is cached until the groups or the language change.
    /// </summary>
    private static readonly Dictionary<string, MarkerGroupInfo> resolvedGroups = [];

    /// <summary>
    /// Drop the resolved group cache, call this whenever groups or the active language changed.
    /// </summary>
    public static void InvalidateCache() => resolvedGroups.Clear();

    /// <summary>
    /// Resolve any group id, built in or user defined.
    /// </summary>
    public static MarkerGroupInfo? Resolve(string? groupId)
    {
        if (string.IsNullOrEmpty(groupId)) return null;
        if (resolvedGroups.TryGetValue(groupId, out var cached)) return cached;

        var info = ResolveUncached(groupId);
        if (info is { } value) resolvedGroups[groupId] = value;

        return info;
    }

    private static MarkerGroupInfo? ResolveUncached(string groupId)
    {
        foreach (var group in All)
        {
            if (group.Id == groupId) {
                return new MarkerGroupInfo(group.Id, group.IconId, Localization.Get(group.NameKey), group.DefaultVisible, true, group.Scale);
            }
        }

        if (System.CustomMarkerConfig is null) return null;

        foreach (var group in System.CustomMarkerConfig.CustomGroups)
        {
            if (group.Id == groupId) {
                return new MarkerGroupInfo(group.Id, group.IconId, group.Name, true, false, group.Scale);
            }
        }

        return null;
    }

    public static IReadOnlyList<CustomMarkerGroup> GetCustomGroups()
        => System.CustomMarkerConfig?.CustomGroups ?? (IReadOnlyList<CustomMarkerGroup>)[];
}
