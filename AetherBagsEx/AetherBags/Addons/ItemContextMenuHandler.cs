using AetherBags.Configuration;
using AetherBags.Extensions;
using AetherBags.Helpers;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using AetherBags.Inventory.State;
using AetherBags.IPC.ExternalCategorySystem;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit.ContextMenu;
using KamiMenu = KamiToolKit.ContextMenu.ContextMenu;

namespace AetherBags.Addons;

/// <summary>
/// Right click menu for a single item. The menu is built by the plugin so its
/// contents are always predictable; the game's own item menu stays available
/// through one entry. Appending to the native menu is not safe here: reopening
/// AgentContext would resurrect whatever menu was shown before (e.g. the
/// category-wide "deposit all / withdraw all" actions).
/// </summary>
public static class ItemContextMenuHandler
{
    private static KamiMenu? _itemMenu;

    public static void Initialize()
    {
        _itemMenu = new KamiMenu();
    }

    public static void Dispose()
    {
        _itemMenu?.Dispose();
        _itemMenu = null;
    }

    /// <summary>
    /// Opens the item menu. Returns false when there is nothing to show, so the
    /// caller can fall back to the game's own item menu.
    /// </summary>
    public static bool TryShowItemMenu(ItemInfo item, bool includeExternalEntries)
    {
        if (_itemMenu == null) return false;

        var entries = includeExternalEntries
            ? ExternalCategoryManager.GetContextMenuEntries(item.Item.ItemId)
            : null;

        var (canDeposit, canWithdraw) = GetAvailableActions(item);
        bool hasTransferActions = canDeposit || canWithdraw;
        bool hasEntries = entries != null && entries.Count > 0;

        if (!hasTransferActions && !hasEntries) return false;

        _itemMenu.Clear();

        if (hasTransferActions) AddTransferItems(_itemMenu, item, canDeposit, canWithdraw);

        _itemMenu.AddItem(new ContextMenuItem
        {
            Name = Localization.Get("ContextMenu.GameMenu"),
            OnClick = () => Services.Framework.RunOnTick(() => OpenNativeItemMenu(item)),
        });

        if (hasEntries)
        {
            var context = new ContextMenuContext(
                item.Item.ItemId,
                (int)item.Item.Container,
                item.Item.Slot
            );

            foreach (var entry in entries!)
            {
                var capturedEntry = entry;
                _itemMenu.AddItem(capturedEntry.Label, () => capturedEntry.OnClick(context));
            }
        }

        _itemMenu.Open();
        return true;
    }

    private static void AddTransferItems(KamiMenu menu, ItemInfo item, bool canDeposit, bool canWithdraw)
    {
        menu.AddItem(new ContextMenuItem
        {
            Name = Localization.Get("ContextMenu.DepositItemToCompanyChest"),
            IsEnabled = canDeposit,
            OnClick = () => Deposit(item),
        });

        menu.AddItem(new ContextMenuItem
        {
            Name = Localization.Get("ContextMenu.WithdrawItemToInventory"),
            IsEnabled = canWithdraw,
            OnClick = () => Withdraw(item),
        });
    }

    private static (bool CanDeposit, bool CanWithdraw) GetAvailableActions(ItemInfo item)
    {
        if (!CompanyChestState.AreContainersLoaded) return (false, false);

        var container = item.Item.Container;

        bool canDeposit = container.IsMainInventory
            && InventoryScanner.GetFirstEmptySlotFromContainers(CompanyChestPages.GetDepositContainers()).IsValid;

        bool canWithdraw = container.IsCompanyChest
            && container != InventoryType.FreeCompanyCrystals
            && InventoryScanner.GetFirstEmptySlot(InventorySourceType.MainBags).IsValid;

        return (canDeposit, canWithdraw);
    }

    /// <summary>
    /// Hands a single item back to the game's own context menu. ClearMenu first so
    /// the entries of the plugin menu are not carried over.
    /// </summary>
    private static unsafe void OpenNativeItemMenu(ItemInfo item)
    {
        var agent = AgentContext.Instance();
        if (agent != null) agent->ClearMenu();

        var context = AgentInventoryContext.Instance();
        if (context == null) return;

        var inventoryItem = item.Item;
        context->OpenForItemSlot(inventoryItem.Container, inventoryItem.Slot, 0, context->AddonId);
    }

    private static void Deposit(ItemInfo item)
        => CategoryTransferQueue.MoveAllToCompanyChest(new[] { item });

    private static void Withdraw(ItemInfo item)
        => CategoryTransferQueue.MoveAllFromCompanyChest(new[] { item });
}
