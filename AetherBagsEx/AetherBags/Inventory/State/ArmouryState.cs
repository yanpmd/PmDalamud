using AetherBags.Configuration;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory.State;

public class ArmouryState : InventoryStateBase
{
    /// <summary>Armoury containers hold 50 slots each.</summary>
    private const int SlotsPerPart = 50;

    public override InventorySourceType SourceType => InventorySourceType.Armoury;

    /// <summary>
    /// The part currently shown. Only that container is scanned, otherwise the
    /// window would have to lay out all 600 armoury slots at once.
    /// </summary>
    public static ArmouryPart CurrentPart
    {
        get => System.Config?.General?.ArmouryPart ?? ArmouryPart.MainHand;
        set
        {
            if (System.Config is null) return;
            System.Config.General.ArmouryPart = value;
        }
    }

    public override InventoryType[] Inventories => ArmouryParts.GetContainers(CurrentPart);

    public override int TotalSlots => Inventories.Length * SlotsPerPart;

    public override int EmptySlots => InventoryScanner.GetEmptySlotsInContainers(Inventories);

    // Only the visible part may receive dropped items, otherwise a weapon could be
    // routed into, say, the ring page.
    public override InventoryLocation GetFirstEmptySlot()
        => InventoryScanner.GetFirstEmptySlotFromContainers(Inventories);

    public static ArmouryGrouping CurrentGrouping
    {
        get => System.Config?.General?.ArmouryGrouping ?? ArmouryGrouping.ByRole;
        set
        {
            if (System.Config is null) return;
            System.Config.General.ArmouryGrouping = value;
        }
    }

    /// <summary>
    /// Groups by class/job instead of the game's item UI category. Grouping the
    /// armoury by slot would show one giant "legs" pile for every job.
    /// </summary>
    protected override void BucketUnclaimedItems(bool userCategoriesEnabled)
    {
        if (CurrentGrouping == ArmouryGrouping.BySlot || !System.Config.Categories.GameCategoriesEnabled)
        {
            base.BucketUnclaimedItems(userCategoriesEnabled);
            return;
        }

        CategoryBucketManager.BucketByJobRole(ItemInfoByKey, BucketsByKey, ClaimedKeys, userCategoriesEnabled);
    }

    protected override void OnPostScan()
    {
        // The armoury pages are sorted by ItemOrderModule, so the visual slot and
        // the storage slot differ. The drag-drop payloads are built from the visual
        // location, which has to be refreshed after every scan.
        InventoryContextState.RefreshMaps();
        InventoryContextState.RefreshBlockedSlots();
    }
}
