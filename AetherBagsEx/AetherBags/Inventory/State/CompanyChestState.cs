using AetherBags.Configuration;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AetherBags.Inventory.State;

public class CompanyChestState : InventoryStateBase
{
    /// <summary>Each company chest tab holds 50 slots.</summary>
    private const int SlotsPerPage = 50;

    public override InventorySourceType SourceType => InventorySourceType.CompanyChest;

    /// <summary>
    /// The chest tab currently shown. Only that page is scanned, matching the game
    /// UI where each chest is a separate 50 slot container.
    /// </summary>
    public static CompanyChestPage CurrentPage
    {
        get => System.Config?.General?.CompanyChestPage ?? CompanyChestPage.Page1;
        set
        {
            if (System.Config is null) return;
            System.Config.General.CompanyChestPage = value;
        }
    }

    public override InventoryType[] Inventories => CompanyChestPages.GetContainers(CurrentPage);

    public override int TotalSlots => Inventories.Length * SlotsPerPage;

    public override int EmptySlots => InventoryScanner.GetEmptySlotsInContainers(Inventories);

    // Drops must land in the visible tab, otherwise an item could be routed into
    // a chest page the player is not looking at.
    public override InventoryLocation GetFirstEmptySlot()
        => InventoryScanner.GetFirstEmptySlotFromContainers(Inventories);

    /// <summary>
    /// True when the free company chest is open and its pages hold items. The
    /// pages only report a size while the game's own chest addon is up.
    /// </summary>
    public static unsafe bool AreContainersLoaded
    {
        get
        {
            if (!Services.ClientState.IsLoggedIn) return false;

            var addonManager = RaptureAtkUnitManager.Instance();
            if (addonManager == null) return false;
            if (addonManager->GetAddonByName("FreeCompanyChest") == null) return false;

            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null) return false;

            foreach (var type in InventorySourceDefinitions.CompanyChest)
            {
                var container = inventoryManager->GetInventoryContainer(type);
                if (container != null && container->Size > 0) return true;
            }

            return false;
        }
    }
}
