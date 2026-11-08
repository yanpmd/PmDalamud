using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Inventory;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Helpers;

/// <summary>
/// Writes the plugin's display order back into the real inventory slots and folds
/// duplicate stacks into each other.
/// <para>
/// The game only knows how to move a single item from one slot to another, so both
/// operations are built as a list of single moves that are dispatched one per tick.
/// The sort never moves onto an occupied slot: the items are shuffled around an
/// empty slot ("hole"), which is the only move the game is known to accept.
/// The merge has to move a stack onto an occupied slot, so every move is verified
/// afterwards and reverted when the game refused it.
/// </para>
/// </summary>
public static unsafe class InventoryOrganizer
{
    public enum OperationKind
    {
        Sort,
        Merge,
    }

    /// <summary>How long a single move may take before it is treated as refused.</summary>
    private const int MoveTimeoutMs = 3000;

    private readonly record struct SlotRef(InventoryType Container, ushort Slot);

    private sealed class StackEntry
    {
        public required SlotRef Slot { get; init; }
        public required int SlotIndex { get; init; }
        public required uint ItemId { get; init; }
        public required bool IsHq { get; init; }
        public required int Quantity { get; set; }
        public required int MaxStack { get; init; }
        public bool IsStackable => MaxStack > 1;
    }

    private sealed class MoveStep
    {
        public required int GroupIndex { get; init; }
        public required SlotRef Source { get; init; }
        public required SlotRef Destination { get; init; }
        public required uint ItemId { get; init; }
        public required int SourceQuantity { get; init; }
        public required int DestinationQuantity { get; init; }
        public required bool IsMerge { get; init; }
        public bool Dispatched { get; set; }
        public long Deadline { get; set; }
    }

    private enum VerifyResult
    {
        Success,
        Pending,
        Failed,
    }

    private enum DispatchState
    {
        Ready,
        NotReady,
        Obsolete,
    }

    private static readonly List<MoveStep> Pending = new();

    private static OperationKind _kind;
    private static int _processed;
    private static int _total;
    private static bool _running;
    private static TimeSpan _stepDelay = TimeSpan.Zero;

    /// <summary>True while a sort or merge is running.</summary>
    public static bool IsRunning => _running;

    /// <summary>Number of finished moves of the running operation.</summary>
    public static int Processed => _processed;

    /// <summary>Number of moves the running operation has planned.</summary>
    public static int Total => _total;

    /// <summary>The operation that is currently running.</summary>
    public static OperationKind Kind => _kind;

    /// <summary>
    /// Reorders the real slots of the given containers so they follow the order the
    /// plugin displays. The display order is the flattened list of categories.
    /// </summary>
    public static bool StartSort(InventoryType[] containers, IReadOnlyList<ItemInfo> displayOrder)
    {
        if (_running) return false;

        containers = FilterOrganizableContainers(containers);
        if (containers.Length == 0) return false;

        var ranks = BuildRanks(displayOrder);
        var groups = BuildGroups(containers);

        Pending.Clear();

        int groupIndex = 0;
        foreach (var group in groups)
        {
            var steps = BuildSortSteps(group, ranks, groupIndex);
            if (steps is not null) Pending.AddRange(steps);
            groupIndex++;
        }

        if (Pending.Count == 0)
        {
            Services.Logger.Information("[Organizer] Nothing to sort.");
            return false;
        }

        _kind = OperationKind.Sort;
        _processed = 0;
        _total = Pending.Count;
        _running = true;
        _stepDelay = ComputeDelay(containers);

        Services.Logger.Information($"[Organizer] Sorting {_total} slots.");
        Services.Framework.RunOnTick(Step);
        return true;
    }

    /// <summary>
    /// Folds duplicate stacks of the given containers into each other. Only items
    /// that can hold more than one are merged, everything else is left alone.
    /// </summary>
    public static bool StartMerge(InventoryType[] containers)
    {
        if (_running) return false;

        containers = FilterOrganizableContainers(containers);
        if (containers.Length == 0) return false;

        var stacks = ScanStacks(containers);
        if (stacks.Count == 0)
        {
            Services.Logger.Information("[Organizer] Nothing to merge.");
            return false;
        }

        Pending.Clear();

        var consumed = new HashSet<StackEntry>();
        int groupIndex = 0;

        foreach (var group in stacks.Where(s => s.IsStackable).GroupBy(s => (s.ItemId, s.IsHq)))
        {
            var members = group.OrderBy(s => s.Quantity).ToList();

            foreach (var source in members)
            {
                if (consumed.Contains(source)) continue;

                // Merge into the fullest stack that still has room and lives in a
                // container the game lets us merge across.
                var target = members
                    .Where(t => t != source
                                && !consumed.Contains(t)
                                && t.Quantity + source.Quantity <= t.MaxStack
                                && CanMergeAcross(t.Slot.Container, source.Slot.Container))
                    .OrderByDescending(t => t.Quantity)
                    .FirstOrDefault();

                if (target is null) continue;

                Pending.Add(new MoveStep
                {
                    GroupIndex = groupIndex,
                    Source = source.Slot,
                    Destination = target.Slot,
                    ItemId = source.ItemId,
                    SourceQuantity = source.Quantity,
                    DestinationQuantity = target.Quantity,
                    IsMerge = true,
                });

                target.Quantity += source.Quantity;
                consumed.Add(source);
            }

            groupIndex++;
        }

        if (Pending.Count == 0)
        {
            Services.Logger.Information("[Organizer] No duplicate stacks found.");
            return false;
        }

        _kind = OperationKind.Merge;
        _processed = 0;
        _total = Pending.Count;
        _running = true;
        _stepDelay = ComputeDelay(containers);

        Services.Logger.Information($"[Organizer] Merging {_total} stacks.");
        Services.Framework.RunOnTick(Step);
        return true;
    }

    /// <summary>Stops a running operation. Moves that already happened stay done.</summary>
    public static void Cancel()
    {
        if (!_running) return;

        Pending.Clear();
        _running = false;
        _processed = 0;
        _total = 0;

        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    /// <summary>
    /// Crystals live in their own container and cannot be moved with a normal item
    /// move, so they are left out of sorting and merging.
    /// </summary>
    private static InventoryType[] FilterOrganizableContainers(InventoryType[] containers)
    {
        var list = new List<InventoryType>(containers.Length);

        foreach (var container in containers)
        {
            if (container == InventoryType.FreeCompanyCrystals) continue;
            list.Add(container);
        }

        return list.ToArray();
    }

    private static Dictionary<(uint ItemId, bool IsHq), int> BuildRanks(IReadOnlyList<ItemInfo> displayOrder)
    {
        var ranks = new Dictionary<(uint, bool), int>();

        int rank = 0;
        foreach (var item in displayOrder)
        {
            var key = (item.Item.ItemId, item.IsHq);
            if (!ranks.ContainsKey(key)) ranks[key] = rank;
            rank++;
        }

        return ranks;
    }

    /// <summary>
    /// The containers are split into groups the game treats as one inventory. Items
    /// can be dragged between the pages of the main bag, the saddlebag and the
    /// retainer, but the company chest tabs and the armoury parts are separate
    /// containers, so those are sorted one by one.
    /// </summary>
    private static List<InventoryType[]> BuildGroups(InventoryType[] containers)
    {
        var groups = new List<InventoryType[]>();
        var current = new List<InventoryType>();
        int currentGroup = 0;

        foreach (var container in containers)
        {
            var group = container.ContainerGroup;

            if (group == 0 || group == 5)
            {
                // The company chest tabs are independent containers.
                if (current.Count > 0)
                {
                    groups.Add(current.ToArray());
                    current.Clear();
                }

                groups.Add([container]);
                currentGroup = 0;
                continue;
            }

            if (currentGroup != 0 && currentGroup != group)
            {
                groups.Add(current.ToArray());
                current.Clear();
            }

            currentGroup = group;
            current.Add(container);
        }

        if (current.Count > 0) groups.Add(current.ToArray());

        return groups;
    }

    private static TimeSpan ComputeDelay(InventoryType[] containers)
    {
        foreach (var container in containers)
        {
            // The company chest is rate limited server side.
            if (container.IsCompanyChest) return CategoryTransferQueue.CompanyChestOperationDelay;
        }

        return TimeSpan.Zero;
    }

    private static List<StackEntry> ScanStacks(InventoryType[] containers)
    {
        var stacks = new List<StackEntry>();
        var manager = InventoryManager.Instance();
        if (manager == null) return stacks;

        foreach (var container in containers)
        {
            var inventory = manager->GetInventoryContainer(container);
            if (inventory == null || inventory->Size == 0) continue;

            for (int slot = 0; slot < inventory->Size; slot++)
            {
                ref var item = ref inventory->Items[slot];
                if (item.ItemId == 0) continue;

                stacks.Add(new StackEntry
                {
                    Slot = new SlotRef(container, (ushort)slot),
                    SlotIndex = slot,
                    ItemId = item.ItemId,
                    IsHq = (item.Flags & InventoryItem.ItemFlags.HighQuality) != 0,
                    Quantity = item.Quantity,
                    MaxStack = GetMaxStack(item.ItemId),
                });
            }
        }

        return stacks;
    }

    private static int GetMaxStack(uint itemId)
    {
        if (!ItemInfo.ItemSheet.TryGetRow(itemId, out var row)) return 1;
        return (int)row.StackSize;
    }

    /// <summary>
    /// True when the game allows the two stacks to be folded into each other.
    /// The armoury parts and the company chest tabs are separate containers, so
    /// only stacks of the very same container can be merged there.
    /// </summary>
    private static bool CanMergeAcross(InventoryType a, InventoryType b)
    {
        if (a == b) return true;

        if (a.IsMainInventory && b.IsMainInventory) return true;

        if (a.IsRetainer && b.IsRetainer) return true;

        if (a.IsSaddleBag && b.IsSaddleBag)
        {
            bool aPremium = a is InventoryType.PremiumSaddleBag1 or InventoryType.PremiumSaddleBag2;
            bool bPremium = b is InventoryType.PremiumSaddleBag1 or InventoryType.PremiumSaddleBag2;
            return aPremium == bPremium;
        }

        return false;
    }

    private static List<MoveStep>? BuildSortSteps(InventoryType[] group, Dictionary<(uint, bool), int> ranks, int groupIndex)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return null;

        var slots = new List<SlotRef>();
        foreach (var container in group)
        {
            var inventory = manager->GetInventoryContainer(container);
            if (inventory == null || inventory->Size == 0) continue;

            for (int slot = 0; slot < inventory->Size; slot++)
                slots.Add(new SlotRef(container, (ushort)slot));
        }

        if (slots.Count == 0) return null;

        var stacks = new List<StackEntry>();
        var occupant = new Dictionary<SlotRef, int>();
        SlotRef? hole = null;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var item = manager->GetInventorySlot(slot.Container, slot.Slot);

            if (item == null || item->ItemId == 0)
            {
                occupant[slot] = -1;
                hole ??= slot;
                continue;
            }

            stacks.Add(new StackEntry
            {
                Slot = slot,
                SlotIndex = i,
                ItemId = item->ItemId,
                IsHq = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0,
                Quantity = item->Quantity,
                MaxStack = GetMaxStack(item->ItemId),
            });

            occupant[slot] = stacks.Count - 1;
        }

        if (stacks.Count < 2) return null;

        // Without a free slot there is nowhere to park an item while it is being
        // shuffled around, so the group is left untouched.
        if (hole is null) return null;

        var ordered = stacks
            .Select((stack, index) => (Stack: stack, Index: index))
            .OrderBy(x => ranks.TryGetValue((x.Stack.ItemId, x.Stack.IsHq), out var rank) ? rank : int.MaxValue)
            .ThenBy(x => x.Index)
            .Select(x => x.Stack)
            .ToList();

        var stackIndex = new Dictionary<StackEntry, int>();
        for (int i = 0; i < stacks.Count; i++) stackIndex[stacks[i]] = i;

        var position = new Dictionary<int, SlotRef>();
        for (int i = 0; i < stacks.Count; i++) position[i] = stacks[i].Slot;

        var steps = new List<MoveStep>();
        var holeSlot = hole.Value;

        for (int k = 0; k < ordered.Count; k++)
        {
            var target = slots[k];
            int wanted = stackIndex[ordered[k]];
            int current = occupant[target];

            if (current == wanted) continue;

            if (current >= 0)
            {
                // Park the current occupant of the target slot in the hole.
                var parked = stacks[current];
                steps.Add(new MoveStep
                {
                    GroupIndex = groupIndex,
                    Source = target,
                    Destination = holeSlot,
                    ItemId = parked.ItemId,
                    SourceQuantity = parked.Quantity,
                    DestinationQuantity = 0,
                    IsMerge = false,
                });

                occupant[holeSlot] = current;
                position[current] = holeSlot;
                occupant[target] = -1;
            }

            // Move the wanted stack into the now empty target slot.
            var source = position[wanted];
            steps.Add(new MoveStep
            {
                GroupIndex = groupIndex,
                Source = source,
                Destination = target,
                ItemId = stacks[wanted].ItemId,
                SourceQuantity = stacks[wanted].Quantity,
                DestinationQuantity = 0,
                IsMerge = false,
            });

            occupant[target] = wanted;
            position[wanted] = target;
            occupant[source] = -1;
            holeSlot = source;
        }

        return steps;
    }

    private static void Step()
    {
        if (!_running) return;

        if (Pending.Count == 0)
        {
            Finish();
            return;
        }

        var step = Pending[0];

        if (!step.Dispatched)
        {
            switch (CheckDispatch(step))
            {
                case DispatchState.Ready:
                    InventoryMoveHelper.MoveItemDirect(step.Source.Container, step.Source.Slot, step.Destination.Container, step.Destination.Slot);

                    step.Dispatched = true;
                    step.Deadline = Environment.TickCount64 + MoveTimeoutMs;

                    Reschedule();
                    return;

                case DispatchState.Obsolete:
                    // Something else moved the item in the meantime, the rest of
                    // the plan does not apply anymore.
                    FailStep(step, "the slot no longer holds the expected item");
                    return;

                default:
                    if (Environment.TickCount64 > step.Deadline)
                    {
                        FailStep(step, "the destination slot is still occupied");
                        return;
                    }

                    Reschedule();
                    return;
            }
        }

        switch (VerifyStep(step))
        {
            case VerifyResult.Success:
                _processed++;
                Pending.RemoveAt(0);
                Reschedule();
                return;

            case VerifyResult.Pending:
                if (Environment.TickCount64 > step.Deadline)
                {
                    FailStep(step, "the game did not apply the move");
                    return;
                }

                Reschedule();
                return;

            default:
                FailStep(step, "the game swapped the stacks instead of merging them");
                return;
        }
    }

    /// <summary>
    /// Checks that the move can be dispatched: the source still holds the item and
    /// the destination is free.
    /// </summary>
    private static DispatchState CheckDispatch(MoveStep step)
    {
        var source = ReadSlot(step.Source.Container, step.Source.Slot);
        if (source == null) return DispatchState.NotReady;
        if (source->ItemId != step.ItemId) return DispatchState.Obsolete;

        var destination = ReadSlot(step.Destination.Container, step.Destination.Slot);
        if (destination == null) return DispatchState.NotReady;

        if (step.IsMerge)
        {
            // The destination holds the stack the source is merged into.
            return destination->ItemId == step.ItemId
                ? DispatchState.Ready
                : DispatchState.Obsolete;
        }

        // A sort move always goes into a free slot.
        return destination->ItemId == 0
            ? DispatchState.Ready
            : DispatchState.NotReady;
    }

    /// <summary>
    /// Reads a slot straight from its container, so every container the game knows
    /// is handled the same way.
    /// </summary>
    private static InventoryItem* ReadSlot(InventoryType container, ushort slot)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return null;

        var inventory = manager->GetInventoryContainer(container);
        if (inventory == null || slot >= inventory->Size) return null;

        return &inventory->Items[slot];
    }

    private static VerifyResult VerifyStep(MoveStep step)
    {
        var source = ReadSlot(step.Source.Container, step.Source.Slot);
        var destination = ReadSlot(step.Destination.Container, step.Destination.Slot);
        if (source == null || destination == null) return VerifyResult.Pending;

        uint sourceItemId = source->ItemId;
        int sourceQuantity = source->Quantity;
        uint destinationItemId = destination->ItemId;
        int destinationQuantity = destination->Quantity;

        if (sourceItemId == 0)
        {
            // The source slot was emptied, so the move went through.
            if (destinationItemId != step.ItemId) return VerifyResult.Pending;
            if (!step.IsMerge) return VerifyResult.Success;
            return destinationQuantity >= step.DestinationQuantity + step.SourceQuantity
                ? VerifyResult.Success
                : VerifyResult.Pending;
        }

        if (sourceItemId != step.ItemId) return VerifyResult.Pending;

        if (step.IsMerge
            && sourceQuantity == step.DestinationQuantity
            && destinationQuantity == step.SourceQuantity)
        {
            // The two stacks were swapped instead of merged. Put them back.
            InventoryMoveHelper.MoveItemDirect(step.Destination.Container, step.Destination.Slot, step.Source.Container, step.Source.Slot);
            return VerifyResult.Failed;
        }

        // The source still holds the item: the move was refused or has not been
        // applied yet.
        return VerifyResult.Pending;
    }

    private static void FailStep(MoveStep step, string reason)
    {
        Services.Logger.Warning(
            $"[Organizer] Skipped a move ({reason}): {step.Source.Container}@{step.Source.Slot} -> {step.Destination.Container}@{step.Destination.Slot}");

        _processed++;

        // The rest of the group was planned around this move, so it cannot run
        // anymore. The other groups are still processed.
        int groupIndex = step.GroupIndex;
        while (Pending.Count > 0 && Pending[0].GroupIndex == groupIndex)
            Pending.RemoveAt(0);

        Reschedule();
    }

    private static void Reschedule()
    {
        if (!_running) return;
        if (_stepDelay > TimeSpan.Zero)
        {
            Services.Framework.RunOnTick(Step, _stepDelay);
            return;
        }

        Services.Framework.RunOnTick(Step);
    }

    private static void Finish()
    {
        bool wasSort = _kind == OperationKind.Sort;
        int processed = _processed;
        int total = _total;

        _running = false;
        _processed = 0;
        _total = 0;
        Pending.Clear();

        Services.Logger.Information(
            wasSort
                ? $"[Organizer] Sort finished, {processed}/{total} moves."
                : $"[Organizer] Merge finished, {processed}/{total} moves.");

        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }
}
