using System;
using System.Collections.Generic;
using System.ComponentModel;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Configuration;

/// <summary>
/// The tabs of the free company chest that hold items: up to five chest pages plus
/// the crystal tab. How many chest pages exist depends on the company rank, so
/// callers must check <see cref="CompanyChestPages.IsAvailable"/> per page. Gil is
/// a value rather than an item container. Deposits treat the unlocked pages as one
/// continuous space: pages are tried in order and a full one spills into the next.
/// </summary>
public enum CompanyChestPage : byte
{
    [Description("Chest 1")]
    Page1 = 0,

    [Description("Chest 2")]
    Page2 = 1,

    [Description("Chest 3")]
    Page3 = 2,

    [Description("Chest 4")]
    Page4 = 3,

    [Description("Chest 5")]
    Page5 = 4,

    [Description("Crystals")]
    Crystals = 5,
}

public static class CompanyChestPages
{
    public static readonly (CompanyChestPage page, string nameKey)[] Items =
    [
        (CompanyChestPage.Page1, "CompanyChest.Page1"),
        (CompanyChestPage.Page2, "CompanyChest.Page2"),
        (CompanyChestPage.Page3, "CompanyChest.Page3"),
        (CompanyChestPage.Page4, "CompanyChest.Page4"),
        (CompanyChestPage.Page5, "CompanyChest.Page5"),
        (CompanyChestPage.Crystals, "CompanyChest.Crystals"),
    ];

    // Shared arrays: Inventories is read on every refresh, scan and drop lookup.
    private static readonly InventoryType[] Page1Containers = [InventoryType.FreeCompanyPage1];
    private static readonly InventoryType[] Page2Containers = [InventoryType.FreeCompanyPage2];
    private static readonly InventoryType[] Page3Containers = [InventoryType.FreeCompanyPage3];
    private static readonly InventoryType[] Page4Containers = [InventoryType.FreeCompanyPage4];
    private static readonly InventoryType[] Page5Containers = [InventoryType.FreeCompanyPage5];
    private static readonly InventoryType[] CrystalContainers = [InventoryType.FreeCompanyCrystals];

    private static InventoryType[] s_availableContainers = Array.Empty<InventoryType>();
    private static int s_availableMask = -1;

    public static string GetName(CompanyChestPage page)
    {
        foreach (var item in Items)
        {
            if (item.page == page) return Localization.Get(item.nameKey);
        }

        return page.ToString();
    }

    /// <summary>
    /// Pages that accept ordinary items, in deposit order. The crystal tab is
    /// excluded because it only holds crystals. Callers search this whole array
    /// for the first empty slot, so a full page spills into the next one.
    /// </summary>
    public static InventoryType[] GetDepositContainers()
    {
        var available = GetAvailableContainers();
        if (available.Length == 0) return Array.Empty<InventoryType>();

        var containers = new List<InventoryType>(available.Length);
        foreach (var type in available)
        {
            if (type != InventoryType.FreeCompanyCrystals) containers.Add(type);
        }

        return containers.ToArray();
    }

    /// <summary>
    /// Every chest page the company has unlocked. Locked pages report a zero sized
    /// container, so they are left out instead of being scanned as empty.
    /// </summary>
    public static InventoryType[] GetAvailableContainers()
    {
        int mask = 0;
        for (int i = 0; i < Items.Length; i++)
        {
            if (IsAvailable(Items[i].page)) mask |= 1 << i;
        }

        if (mask == s_availableMask) return s_availableContainers;

        s_availableMask = mask;

        var containers = new List<InventoryType>(Items.Length);
        for (int i = 0; i < Items.Length; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            containers.AddRange(GetContainers(Items[i].page));
        }

        s_availableContainers = containers.ToArray();
        return s_availableContainers;
    }

    /// <summary>
    /// True when the company has unlocked this tab. Chest pages are granted by
    /// company rank, and a locked page reports a zero sized container.
    /// </summary>
    public static unsafe bool IsAvailable(CompanyChestPage page)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return false;

        foreach (var type in GetContainers(page))
        {
            var container = manager->GetInventoryContainer(type);
            if (container != null && container->Size > 0) return true;
        }

        return false;
    }

    public static InventoryType[] GetContainers(CompanyChestPage page) => page switch
    {
        CompanyChestPage.Page1 => Page1Containers,
        CompanyChestPage.Page2 => Page2Containers,
        CompanyChestPage.Page3 => Page3Containers,
        CompanyChestPage.Page4 => Page4Containers,
        CompanyChestPage.Page5 => Page5Containers,
        CompanyChestPage.Crystals => CrystalContainers,
        _ => InventorySourceDefinitions.CompanyChest,
    };
}
