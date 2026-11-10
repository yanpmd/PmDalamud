using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory.State;

public class MainBagState :  InventoryStateBase
{
    public override InventorySourceType SourceType => InventorySourceType.MainBags;
    public override InventoryType[] Inventories => InventorySourceDefinitions.MainBags;

    /// <summary>
    /// Weapons and tools only need four piles here: main/off hand and weapon/tool.
    /// The game's per weapon type categories (one handed sword, great axe, ...)
    /// scatter a single weapon slot over a dozen headers.
    /// </summary>
    protected override void BucketUnclaimedItems(bool userCategoriesEnabled)
    {
        if (!System.Config.Categories.GameCategoriesEnabled)
        {
            base.BucketUnclaimedItems(userCategoriesEnabled);
            return;
        }

        CategoryBucketManager.BucketByGameCategories(
            ItemInfoByKey,
            BucketsByKey,
            ClaimedKeys,
            userCategoriesEnabled,
            mergeWeaponSlots: true);
    }

    protected override void OnPostScan()
    {
        InventoryContextState.RefreshMaps();
        InventoryContextState.RefreshBlockedSlots();
    }
}