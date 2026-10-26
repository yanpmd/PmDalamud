using System.Collections.Generic;
using AetherBags.Hooks;
using AetherBags.Inventory;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Helpers;

public enum CategoryTransferKind
{
    ToSaddleBag,
    FromSaddleBag,
    ToRetainer,
    FromRetainer,
}

/// <summary>
/// Moves every stack of a single category to or from the saddlebag or the active
/// retainer. Moves are dispatched one per tick: the game only updates a slot
/// after the previous request has been accepted, so bursting a whole category in
/// one frame would target slots that no longer hold the stack we snapshotted.
/// </summary>
public static class CategoryTransferQueue
{
    private readonly record struct PendingMove(InventoryType Container, ushort Slot, uint ItemId);

    private static readonly Queue<PendingMove> Pending = new();
    private static CategoryTransferKind _kind;
    private static InventorySourceType _destinationSource;
    private static bool _running;

    /// <summary>Moves the given items from the player inventory into the saddlebag.</summary>
    public static bool MoveAllToSaddleBag(IEnumerable<ItemInfo> items)
        => Start(items, CategoryTransferKind.ToSaddleBag);

    /// <summary>Moves the given items from the saddlebag into the player inventory.</summary>
    public static bool MoveAllFromSaddleBag(IEnumerable<ItemInfo> items)
        => Start(items, CategoryTransferKind.FromSaddleBag);

    /// <summary>Hands the given items over to the active retainer.</summary>
    public static bool MoveAllToRetainer(IEnumerable<ItemInfo> items)
        => Start(items, CategoryTransferKind.ToRetainer);

    /// <summary>Takes the given items back from the active retainer.</summary>
    public static bool MoveAllFromRetainer(IEnumerable<ItemInfo> items)
        => Start(items, CategoryTransferKind.FromRetainer);

    public static bool IsRunning => _running;

    private static bool Start(IEnumerable<ItemInfo> items, CategoryTransferKind kind)
    {
        if (_running) return false;

        // Categories can aggregate stacks from other containers (crystals,
        // external sources, ...); only move stacks that sit on the expected side.
        bool IsOnExpectedSide(InventoryType container) => kind switch
        {
            CategoryTransferKind.ToSaddleBag or CategoryTransferKind.ToRetainer => container.IsMainInventory,
            CategoryTransferKind.FromSaddleBag => container.IsSaddleBag,
            CategoryTransferKind.FromRetainer => container.IsRetainer,
            _ => false,
        };

        Pending.Clear();

        foreach (var item in items)
        {
            var inventoryItem = item.Item;
            if (inventoryItem.ItemId == 0) continue;
            if (!IsOnExpectedSide(inventoryItem.Container)) continue;

            Pending.Enqueue(new PendingMove(inventoryItem.Container, (ushort)inventoryItem.Slot, inventoryItem.ItemId));
        }

        if (Pending.Count == 0) return false;

        _kind = kind;
        _destinationSource = kind switch
        {
            CategoryTransferKind.ToSaddleBag => InventorySourceType.AllSaddleBags,
            _ => InventorySourceType.MainBags,
        };

        if (!HasDestinationSlot())
        {
            Services.Logger.Warning($"[CategoryTransfer] No empty slot available for {kind}.");
            Pending.Clear();
            return false;
        }

        _running = true;
        Services.Framework.RunOnTick(Step);
        return true;
    }

    private static bool HasDestinationSlot() => _kind switch
    {
        CategoryTransferKind.ToSaddleBag => InventoryScanner.GetFirstEmptySlot(InventorySourceType.AllSaddleBags).IsValid,
        CategoryTransferKind.ToRetainer => InventoryScanner.GetFirstEmptySlot(InventorySourceType.Retainer).IsValid,
        CategoryTransferKind.FromSaddleBag or CategoryTransferKind.FromRetainer
            => InventoryScanner.GetFirstEmptySlot(InventorySourceType.MainBags).IsValid,
        _ => false,
    };

    private static unsafe void Step()
    {
        if (!_running) return;

        if (Pending.Count == 0)
        {
            Finish();
            return;
        }

        var manager = InventoryManager.Instance();
        if (manager == null)
        {
            Finish();
            return;
        }

        var next = Pending.Dequeue();
        var source = manager->GetInventorySlot(next.Container, next.Slot);

        // The slot changed underneath us (item moved/consumed); stop instead of
        // relocating an unexpected stack.
        if (source == null || source->ItemId != next.ItemId)
        {
            Finish();
            return;
        }

        bool moved = _kind switch
        {
            CategoryTransferKind.ToSaddleBag or CategoryTransferKind.FromSaddleBag => MoveViaSlotSwap(next),
            CategoryTransferKind.ToRetainer => RetainerCommands.TryEntrust(next.Container, next.Slot),
            CategoryTransferKind.FromRetainer => RetainerCommands.TryRetrieve(next.Container, next.Slot),
            _ => false,
        };

        if (!moved)
        {
            Finish();
            return;
        }

        Services.Framework.RunOnTick(Step);
    }

    private static bool MoveViaSlotSwap(PendingMove move)
    {
        var destination = InventoryScanner.GetFirstEmptySlot(_destinationSource);
        if (!destination.IsValid)
        {
            Services.Logger.Warning($"[CategoryTransfer] No empty slot available in {_destinationSource}, stopping.");
            return false;
        }

        InventoryMoveHelper.MoveItemDirect(move.Container, move.Slot, destination.Container, destination.Slot);
        return true;
    }

    private static void Finish()
    {
        _running = false;
        Pending.Clear();
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }
}
