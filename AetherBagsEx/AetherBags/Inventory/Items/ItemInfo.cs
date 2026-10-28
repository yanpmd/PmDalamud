using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AetherBags.Extensions;
using AetherBags.Helpers;
using AetherBags.Inventory.Context;
using AetherBags.IPC.ExternalCategorySystem;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AetherBags.Inventory.Items;

public sealed class ItemInfo : IEquatable<ItemInfo>
{
    public required ulong Key { get; set; }

    public required InventoryItem Item { get; set; }
    public required int ItemCount { get; set; }

    private static ExcelSheet<Item>? s_itemSheet;
    private static ExcelSheet<EventItem>? s_eventItemSheet;
    internal static ExcelSheet<Item> ItemSheet => s_itemSheet ??= Services.DataManager.GetExcelSheet<Item>();
    internal static ExcelSheet<EventItem> EventItemSheet => s_eventItemSheet ??= Services.DataManager.GetExcelSheet<EventItem>();

    public static bool IsAggregatable(uint itemId)
        => ItemSheet.TryGetRow(itemId, out var row) && row.StackSize > 1;

    private bool _rowLoaded;
    private bool _rowFound;
    private Item _row;

    private bool _eventRowLoaded;
    private bool _eventRowFound;
    private EventItem _eventRow;

    private string? _name;
    private string? _description;
    private string? _levelString;
    private string? _itemLevelString;

    private int _cachedHighlightVersion = -1;
    private float _cachedVisualAlpha;
    private Vector3 _cachedHighlightColor;
    private bool _cachedIsRelationshipHighlighted;

    // Default Lumina row's internal pool ref is null; accessors must guard before touching _row.
    private bool HasRow
    {
        get
        {
            if (!_rowLoaded)
            {
                _rowFound = ItemSheet.TryGetRow(Item.ItemId, out _row);
                _rowLoaded = true;
            }
            return _rowFound;
        }
    }

    private bool HasEventRow
    {
        get
        {
            if (!_eventRowLoaded)
            {
                _eventRowFound = EventItemSheet.TryGetRow(Item.ItemId, out _eventRow);
                _eventRowLoaded = true;
            }
            return _eventRowFound;
        }
    }

    public Vector4 RarityColor => HasRow ? _row.RarityColor : Vector4.One;

    public uint IconId
    {
        get
        {
            if (HasRow) return _row.Icon;
            if (HasEventRow) return _eventRow.Icon;
            return 0u;
        }
    }

    public string Name
    {
        get
        {
            if (_name != null) return _name;
            if (HasRow) return _name = _row.Name.ToString();
            if (HasEventRow) return _name = _eventRow.Name.ToString();
            return _name = string.Empty;
        }
    }

    public int Level => HasRow ? _row.LevelEquip : 0;
    public int ItemLevel => HasRow ? (int)_row.LevelItem.RowId : 0;
    private string LevelString => _levelString ??= Level.ToString();
    private string ItemLevelString => _itemLevelString ??= ItemLevel.ToString();
    public int Rarity => HasRow ? _row.Rarity : 0;
    public uint VendorPrice => HasRow ? _row.PriceLow : 0u;
    public uint StackSize => HasRow ? _row.StackSize : 0u;

    public RowRef<ItemUICategory> UiCategory => HasRow ? _row.ItemUICategory : default;

    /// <summary>Which class/job category may equip this item.</summary>
    public RowRef<ClassJobCategory> ClassJobCategory => HasRow ? _row.ClassJobCategory : default;

    public uint ClassJobCategoryId => ClassJobCategory.RowId;

    /// <summary>
    /// True when the class/job category of this item includes the job the player
    /// is currently on. Used to dim armoury items the current job cannot equip,
    /// the same way the vanilla armoury board does.
    /// </summary>
    public bool IsUsableByCurrentJob
    {
        get
        {
            try
            {
                var rowRef = ClassJobCategory;
                if (rowRef.RowId == 0) return true;
                if (rowRef.ValueNullable is not { } row) return true;

                uint jobId = Services.PlayerState.ClassJob.RowId;
                return jobId == 0 || JobMatches(row, jobId);
            }
            catch
            {
                // Never let a data lookup failure take the window down.
                return true;
            }
        }
    }

    private static bool JobMatches(ClassJobCategory row, uint jobId) => jobId switch
    {
        1 => row.GLA, 2 => row.PGL, 3 => row.MRD, 4 => row.LNC, 5 => row.ARC, 6 => row.CNJ, 7 => row.THM,
        8 => row.CRP, 9 => row.BSM, 10 => row.ARM, 11 => row.GSM, 12 => row.LTW, 13 => row.WVR,
        14 => row.ALC, 15 => row.CUL, 16 => row.MIN, 17 => row.BTN, 18 => row.FSH,
        19 => row.PLD, 20 => row.MNK, 21 => row.WAR, 22 => row.DRG, 23 => row.BRD, 24 => row.WHM,
        25 => row.BLM, 26 => row.ACN, 27 => row.SMN, 28 => row.SCH, 29 => row.ROG, 30 => row.NIN,
        31 => row.MCH, 32 => row.DRK, 33 => row.AST, 34 => row.SAM, 35 => row.RDM, 36 => row.BLU,
        37 => row.GNB, 38 => row.DNC, 39 => row.RPR, 40 => row.SGE, 41 => row.VPR, 42 => row.PCT,
        _ => true,
    };

    public bool IsUntradable => HasRow && _row.IsUntradable;
    public bool IsUnique => HasRow && _row.IsUnique;
    public bool IsMarketable => HasRow && _row.ItemSearchCategory.RowId != 0;
    public bool IsCollectable => HasRow && _row.IsCollectable;
    public bool IsDyeable => HasRow && _row.DyeCount > 0;
    public bool IsRepairable => HasRow && _row.ItemRepair.RowId != 0;

    public bool IsHq => Item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);
    public bool IsDesynthesizable => HasRow && _row.Desynth > 0;
    public bool IsCraftable => HasRow && (_row.ItemAction.RowId != 0 || _row.CanBeHq);
    public bool IsGlamourable => HasRow && _row.IsGlamorous;
    public bool IsSpiritbonded => Item.SpiritbondOrCollectability >= 10000; // 100% = 10000

    private string Description => _description ??= HasRow ? _row.Description.ToString() : string.Empty;

    public InventoryMappedLocation VisualLocation => InventoryContextState.GetVisualLocation(Item.Container, Item.Slot);


    public int InventoryPage => Item.Container switch
    {
        InventoryType.Inventory1 => 0,
        InventoryType.Inventory2 => 1,
        InventoryType.Inventory3 => 2,
        InventoryType.Inventory4 => 3,
        _ => -1
    };

    public bool IsSlotBlocked => InventoryContextState.IsSlotBlocked(Item.Container, Item.Slot);

    public bool IsEligibleForContext
    {
        get
        {
            if (IsSlotBlocked) return false;
            if (!CheckNativeContextEligibility()) return false;
            if (!HighlightState.IsInActiveFilters(Item.ItemId)) return false;

            return true;
        }
    }

    public float VisualAlpha
    {
        get
        {
            EnsureVisualStateCached();
            return _cachedVisualAlpha;
        }
    }

    public Vector3 HighlightOverlayColor
    {
        get
        {
            EnsureVisualStateCached();
            return _cachedHighlightColor;
        }
    }

    public bool IsRelationshipHighlighted
    {
        get
        {
            EnsureVisualStateCached();
            return _cachedIsRelationshipHighlighted;
        }
    }

    private void EnsureVisualStateCached()
    {
        int currentVersion = HighlightState.Version;
        if (_cachedHighlightVersion == currentVersion)
            return;

        _cachedVisualAlpha = IsEligibleForContext ? 1.0f : 0.4f;
        _cachedHighlightColor = HighlightState.GetLabelColor(Item.ItemId) ?? Vector3.Zero;

        var entry = HighlightState.GetHighlightEntry(Item.ItemId);
        _cachedIsRelationshipHighlighted = entry != null;

        _cachedHighlightVersion = currentVersion;
    }

    private bool CheckNativeContextEligibility()
    {
        // The armoury board has no AgentInventory context, so it is handled before
        // the context checks below: only gear the current job can equip is lit.
        if (Item.Container.IsArmory)
            return IsUsableByCurrentJob;

        uint contextId = InventoryContextState.ActiveContextId;
        if (contextId == 0) return true;

        bool isRetainerContext = contextId == 4;
        bool isSaddlebagContext = contextId == 29;
        bool isMainContext = !isRetainerContext && isSaddlebagContext == false;

        if (IsMainInventory)
        {
            if (!isMainContext) return true;
            return InventoryContextState.IsEligible(InventoryPage, Item.Slot);
        }

        if (Item.Container.IsRetainer)
        {
            if (!isRetainerContext) return true;
        }

        if (Item.Container.IsSaddleBag)
        {
            if (!isSaddlebagContext) return true;
        }

        return true;
    }

    public bool IsMainInventory => InventoryPage >= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRegexMatch(string searchTerms)
    {
        if (string.IsNullOrEmpty(searchTerms))
            return true;

        var re = RegexCache.GetOrCreate(searchTerms, compiled: false);
        if (re == null)
            return false;

        if (ExternalCategoryManager.MatchesSearchTag(Item.ItemId, searchTerms)) return true;
        return IsRegexMatch(re);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRegexMatch(Regex re)
    {
        if (re.IsMatch(Name)) return true;
        if (re.IsMatch(LevelString)) return true;
        if (re.IsMatch(ItemLevelString)) return true;
        if (re.IsMatch(Description)) return true;
        return false;
    }

    public bool DescriptionContains(string value)
        => Description.Contains(value, StringComparison.OrdinalIgnoreCase);

    public bool Equals(ItemInfo? other)
        => other is not null && Key == other.Key;

    public override bool Equals(object? obj)
        => obj is ItemInfo other && Equals(other);

    public override int GetHashCode()
        => Key.GetHashCode();
}
