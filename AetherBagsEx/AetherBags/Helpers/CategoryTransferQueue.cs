using System;
using System.Collections.Generic;
using AetherBags.Configuration;
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
    FromArmoury,
    ToCompanyChest,
    FromCompanyChest,
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

    /// <summary>
    /// Interval between company chest operations. The server throttles them, so
    /// transfers and the organizer both walk the chest step by step.
    /// </summary>
    internal static readonly TimeSpan CompanyChestOperationDelay = TimeSpan.FromSeconds(0.5);

    private static readonly Queue<PendingMove> Pending = new();
    private static CategoryTransferKind _kind;
    private static InventorySourceType _destinationSource;
    private static InventoryType[]? _destinationContainers;
    private static bool _running;
    private static int _processed;
    private static int _total;

    /// <summary>Stacks already dispatched in the current batch.</summary>
    public static int Processed => _processed;

    /// <summary>Stacks the current batch is trying to move.</summary>
    public static int Total => _total;

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

    /// <summary>Takes the given items out of the armoury chest into the player inventory.</summary>
    public static bool MoveAllFromArmoury(IEnumerable<ItemInfo> items)
        => Start(items, CategoryTransferKind.FromArmoury);

    /// <summary>Moves the given items from the player inventory into the company chest.</summary>
    public static bool MoveAllToCompanyChest(IEnumerable<ItemInfo> items)
        => Start(items, CategoryTransferKind.ToCompanyChest);

    /// <summary>Takes the given items out of the company chest into the player inventory.</summary>
    public static bool MoveAllFromCompanyChest(IEnumerable<ItemInfo> items)
        => Start(items, CategoryTransferKind.FromCompanyChest);

    public static bool IsRunning => _running;

    private static bool Start(IEnumerable<ItemInfo> items, CategoryTransferKind kind)
    {
        // Repeated single-item transfers join a running batch in the same
        // direction instead of being silently dropped by the chest throttle.
        if (_running && _kind != kind) return false;

        // Categories can aggregate stacks from other containers (crystals,
        // external sources, ...); only move stacks that sit on the expected side.
        bool IsOnExpectedSide(InventoryType container) => kind switch
        {
            CategoryTransferKind.ToSaddleBag or CategoryTransferKind.ToRetainer
                or CategoryTransferKind.ToCompanyChest => container.IsMainInventory,
            CategoryTransferKind.FromSaddleBag => container.IsSaddleBag,
            CategoryTransferKind.FromRetainer => container.IsRetainer,
            CategoryTransferKind.FromArmoury => container.IsArmory,
            // Crystals live in their own container and cannot be moved with a
            // normal item move, so they are left where they are.
            CategoryTransferKind.FromCompanyChest => container.IsCompanyChest
                && container != InventoryType.FreeCompanyCrystals,
            _ => false,
        };

        if (!_running) Pending.Clear();

        int added = 0;
        foreach (var item in items)
        {
            var inventoryItem = item.Item;
            if (inventoryItem.ItemId == 0) continue;
            if (!IsOnExpectedSide(inventoryItem.Container)) continue;

            Pending.Enqueue(new PendingMove(inventoryItem.Container, (ushort)inventoryItem.Slot, inventoryItem.ItemId));
            added++;
        }

        if (added == 0) return false;

        if (_running)
        {
            _total = _processed + Pending.Count;
            return true;
        }

        _kind = kind;
        _destinationSource = kind switch
        {
            CategoryTransferKind.ToSaddleBag => InventorySourceType.AllSaddleBags,
            CategoryTransferKind.ToCompanyChest => InventorySourceType.CompanyChest,
            _ => InventorySourceType.MainBags,
        };

        // Deposits ignore tab boundaries: pages are tried in order and a full
        // page automatically spills over into the next unlocked one.
        _destinationContainers = kind == CategoryTransferKind.ToCompanyChest
            ? CompanyChestPages.GetDepositContainers()
            : null;

        if (!HasDestinationSlot())
        {
            Services.Logger.Warning($"[CategoryTransfer] No empty slot available for {kind}.");
            Pending.Clear();
            return false;
        }

        _running = true;
        _processed = 0;
        _total = Pending.Count;

        Services.Framework.RunOnTick(Step);
        return true;
    }

    private static bool HasDestinationSlot() => _kind switch
    {
        CategoryTransferKind.ToSaddleBag => InventoryScanner.GetFirstEmptySlot(InventorySourceType.AllSaddleBags).IsValid,
        CategoryTransferKind.ToRetainer => InventoryScanner.GetFirstEmptySlot(InventorySourceType.Retainer).IsValid,
        CategoryTransferKind.ToCompanyChest => _destinationContainers is not null
            && InventoryScanner.GetFirstEmptySlotFromContainers(_destinationContainers).IsValid,
        CategoryTransferKind.FromSaddleBag or CategoryTransferKind.FromRetainer
            or CategoryTransferKind.FromArmoury or CategoryTransferKind.FromCompanyChest
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
            CategoryTransferKind.ToSaddleBag or CategoryTransferKind.FromSaddleBag
                or CategoryTransferKind.FromArmoury or CategoryTransferKind.ToCompanyChest
                or CategoryTransferKind.FromCompanyChest => MoveViaSlotSwap(next),
            CategoryTransferKind.ToRetainer => RetainerCommands.TryEntrust(next.Container, next.Slot),
            CategoryTransferKind.FromRetainer => RetainerCommands.TryRetrieve(next.Container, next.Slot),
            _ => false,
        };

        if (!moved)
        {
            Finish();
            return;
        }

        _processed++;

        // The company chest is rate limited server side, a half second between
        // moves keeps the batch from being rejected. Other destinations accept
        // one move per tick.
        if (_kind is CategoryTransferKind.ToCompanyChest or CategoryTransferKind.FromCompanyChest)
        {
            // A single item should refresh the window right away instead of
            // waiting out the throttle for a step that will never move anything.
            if (Pending.Count == 0) Finish();
            else Services.Framework.RunOnTick(Step, CompanyChestOperationDelay);
        }
        else
        {
            Services.Framework.RunOnTick(Step);
        }
    }

    private static bool MoveViaSlotSwap(PendingMove move)
    {
        var destination = _destinationContainers is not null
            ? InventoryScanner.GetFirstEmptySlotFromContainers(_destinationContainers)
            : InventoryScanner.GetFirstEmptySlot(_destinationSource);

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
        _destinationContainers = null;
        _processed = 0;
        _total = 0;
        Pending.Clear();
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }
}
