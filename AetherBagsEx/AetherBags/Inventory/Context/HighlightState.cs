using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace AetherBags.Inventory.Context;

public enum HighlightSource
{
    Search,
    AllaganTools,
    BiSBuddy,
    Relationship,
    RecentlyLooted,
}

public record HighlightEntry(uint ItemId, Vector3 Color);

public static class HighlightState
{
    private static readonly Dictionary<HighlightSource, HashSet<uint>> Filters = new();
    private static readonly Dictionary<HighlightSource, (HashSet<uint> ids, Vector3 color)> Labels = new();
    private static readonly Dictionary<HighlightSource, Dictionary<uint, HighlightEntry>> PerItemLabels = new();

    // Flat cache for O(1) lookups
    private static readonly Dictionary<uint, HighlightEntry> CachedEntries = new(capacity: 512);
    private static bool _cacheValid;
    private static int _version;

    /// <summary>
    /// Version counter that increments when highlight state changes.
    /// Used by ItemInfo to detect when cached visual state is stale.
    /// </summary>
    public static int Version => _version;

    public static string? SelectedAllaganToolsFilterKey { get; set; } = string.Empty;
    public static string? SelectedBisBuddyFilterKey { get; set; } = string.Empty;

    public static bool IsFilterActive => Filters.Count > 0;

    public static void SetFilter(HighlightSource source, IEnumerable<uint> ids)
    {
        // Filters are refreshed on every inventory refresh. Rebuilding the set
        // and bumping the version invalidates the visual state of every item,
        // so skip it when the content is unchanged.
        if (Filters.TryGetValue(source, out var existing) && existing.SetEquals(ids)) return;

        Filters[source] = new HashSet<uint>(ids);
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInActiveFilters(uint itemId)
    {
        if (Filters.Count == 0) return true;
        foreach (var filter in Filters.Values)
            if (filter.Contains(itemId)) return true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HighlightEntry? GetHighlightEntry(uint itemId)
    {
        EnsureCacheValid();
        return CachedEntries.TryGetValue(itemId, out var entry) ? entry : null;
    }

    private static void EnsureCacheValid()
    {
        if (_cacheValid) return;

        CachedEntries.Clear();

        // PerItemLabels have priority - add them first
        foreach (var perItemLabel in PerItemLabels.Values)
        {
            foreach (var (id, entry) in perItemLabel)
            {
                CachedEntries.TryAdd(id, entry);
            }
        }

        // Labels are fallback - only add if not already present
        foreach (var label in Labels.Values)
        {
            var color = label.color;
            foreach (var id in label.ids)
            {
                CachedEntries.TryAdd(id, new HighlightEntry(id, color));
            }
        }

        _cacheValid = true;
    }

    private static void InvalidateCache()
    {
        _cacheValid = false;
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3? GetLabelColor(uint itemId)
        => GetHighlightEntry(itemId)?.Color;

    public static void SetLabel(HighlightSource source, IEnumerable<uint> ids, Vector3 color)
    {
        // Refreshes re-apply labels with the same content, so only invalidate when
        // the effective state actually changes.
        if (!PerItemLabels.ContainsKey(source) &&
            Labels.TryGetValue(source, out var current) &&
            current.color == color &&
            current.ids.SetEquals(ids))
            return;

        PerItemLabels.Remove(source);
        Labels[source] = (new HashSet<uint>(ids), color);
        InvalidateCache();
    }

    public static void SetLabelWithColors(HighlightSource source, Dictionary<uint, Vector4> itemColors)
    {
        var entries = new Dictionary<uint, HighlightEntry>(itemColors.Count);
        foreach (var (itemId, color) in itemColors)
        {
            var rgb = new Vector3(
                color.X * color.W,
                color.Y * color.W,
                color.Z * color.W
            );
            entries[itemId] = new HighlightEntry(itemId, rgb);
        }

        if (!Labels.ContainsKey(source) && IsUnchanged(source, entries)) return;

        Labels.Remove(source);
        PerItemLabels[source] = entries;
        InvalidateCache();
    }

    public static void SetLabelWithColors(HighlightSource source, IEnumerable<HighlightEntry> entries)
    {
        var dict = new Dictionary<uint, HighlightEntry>();
        foreach (var entry in entries)
        {
            dict[entry.ItemId] = entry;
        }

        if (!Labels.ContainsKey(source) && IsUnchanged(source, dict)) return;

        Labels.Remove(source);
        PerItemLabels[source] = dict;
        InvalidateCache();
    }

    public static void SetLabelWithColors(HighlightSource source, Dictionary<uint, Vector3> itemColors)
    {
        var entries = new Dictionary<uint, HighlightEntry>(itemColors.Count);
        foreach (var (itemId, color) in itemColors)
        {
            entries[itemId] = new HighlightEntry(itemId, color);
        }

        if (!Labels.ContainsKey(source) && IsUnchanged(source, entries)) return;

        Labels.Remove(source);
        PerItemLabels[source] = entries;
        InvalidateCache();
    }

    private static bool IsUnchanged(HighlightSource source, Dictionary<uint, HighlightEntry> entries)
    {
        if (!PerItemLabels.TryGetValue(source, out var existing)) return false;
        if (existing.Count != entries.Count) return false;

        foreach (var (itemId, entry) in entries)
        {
            if (!existing.TryGetValue(itemId, out var current) || current.Color != entry.Color)
                return false;
        }

        return true;
    }

    public static void ClearAll()
    {
        var hadState = Filters.Count > 0 || Labels.Count > 0 || PerItemLabels.Count > 0;
        if (!hadState && CachedEntries.Count == 0)
        {
            SelectedAllaganToolsFilterKey = string.Empty;
            return;
        }

        Filters.Clear();
        Labels.Clear();
        PerItemLabels.Clear();
        CachedEntries.Clear();
        _cacheValid = true; // Empty cache is valid
        _version++;
        SelectedAllaganToolsFilterKey = string.Empty;
    }

    public static void ClearFilter(HighlightSource source)
    {
        if (!Filters.Remove(source)) return;
        _version++;
    }

    public static void ClearLabel(HighlightSource source)
    {
        var removed = PerItemLabels.Remove(source);
        removed |= Labels.Remove(source);
        if (removed) InvalidateCache();
    }

    public static void SetRelationshipHighlight(HashSet<uint>? relatedItemIds, Vector3? color)
    {
        if (relatedItemIds == null || relatedItemIds.Count == 0)
        {
            ClearLabel(HighlightSource.Relationship);
            return;
        }

        SetLabel(HighlightSource.Relationship, relatedItemIds, color ?? Vector3.Zero);
    }
}