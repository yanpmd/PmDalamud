using System;
using System.Collections.Generic;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AetherBags.Helpers;

/// <summary>
/// Resolves whether a collectable item (orchestrion roll, triple triad card, mount, minion, ...)
/// has already been unlocked, so its icon can show the game's native "collected" checkmark.
/// </summary>
public static class CollectionStatus
{
    private const long CacheLifetimeMs = 2000;

    private static readonly Dictionary<uint, bool> Cache = new();
    private static long _cacheResetTick;

    public static bool IsCollected(uint itemId)
    {
        if (itemId == 0)
            return false;

        // Unlocks can happen while the window is open (using a roll right after looting it),
        // so the cache is short-lived instead of lasting for the whole session.
        long now = Environment.TickCount64;
        if (now - _cacheResetTick > CacheLifetimeMs)
        {
            Cache.Clear();
            _cacheResetTick = now;
        }

        if (Cache.TryGetValue(itemId, out bool cached))
            return cached;

        bool collected = Resolve(itemId);
        Cache[itemId] = collected;

        return collected;
    }

    private static bool Resolve(uint itemId)
    {
        var item = Services.DataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        if (item is null)
            return false;

        // IsItemUnlockable limits the check to items that actually have a collection state
        // (rolls, cards, mounts, minions, ...); plain gear and materials have none.
        return Services.UnlockState.IsItemUnlockable(item.Value)
            && Services.UnlockState.IsItemUnlocked(item.Value);
    }
}
