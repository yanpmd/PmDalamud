using AetherBags.Configuration;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AetherBags.Inventory.State;

public unsafe class CompanyChestState : InventoryStateBase
{
    public override InventorySourceType SourceType => InventorySourceType.CompanyChest;

    /// <summary>
    /// Every unlocked chest tab is scanned, so all items can be shown in one list.
    /// </summary>
    public override InventoryType[] Inventories => CompanyChestPages.GetAvailableContainers();

    public override int TotalSlots
    {
        get
        {
            var manager = InventoryManager.Instance();
            if (manager == null) return 0;

            int total = 0;
            foreach (var type in Inventories)
            {
                var container = manager->GetInventoryContainer(type);
                if (container == null) continue;
                total += container->Size;
            }

            return total;
        }
    }

    public override int EmptySlots => InventoryScanner.GetEmptySlotsInContainers(Inventories);

    /// <summary>
    /// Deposits and drops treat the chest as one continuous space: the first
    /// empty slot is taken from page 1, then page 2, and so on.
    /// </summary>
    public override InventoryLocation GetFirstEmptySlot()
        => InventoryScanner.GetFirstEmptySlotFromContainers(CompanyChestPages.GetDepositContainers());

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
