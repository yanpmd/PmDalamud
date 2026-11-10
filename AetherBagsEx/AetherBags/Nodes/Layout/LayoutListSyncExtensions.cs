using System;
using System.Collections.Generic;
using System.Linq;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Layout;

/// <summary>
/// Extension methods that restore the list synchronisation helpers that KamiToolKit removed
/// together with its old <c>LayoutListNode</c> implementation.
/// </summary>
public static class LayoutListSyncExtensions {

    /// <summary>
    /// Synchronises the nodes of a layout list with the given data set, keyed by a common key.
    /// <para>
    /// Nodes whose key is no longer present in <paramref name="dataList"/> are removed, missing nodes are created
    /// through <paramref name="createNodeMethod"/> and the remaining nodes are updated in place. The nodes are then
    /// reordered so that they follow the order of <paramref name="dataList"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Type of the data items.</typeparam>
    /// <typeparam name="TU">Type of the nodes holding the data.</typeparam>
    /// <typeparam name="TKey">Type of the key used to match data and nodes.</typeparam>
    /// <param name="list">The list to synchronise.</param>
    /// <param name="dataList">The data to synchronise against.</param>
    /// <param name="getKeyFromData">Extracts the key of a data item.</param>
    /// <param name="getKeyFromNode">Extracts the key of a node.</param>
    /// <param name="updateNode">Applies a data item onto an existing node.</param>
    /// <param name="createNodeMethod">Creates a node for a data item.</param>
    /// <param name="keyComparer">Optional comparer used for the keys.</param>
    /// <returns>True if the structure of the list changed.</returns>
    public static bool SyncWithListDataByKey<T, TU, TKey>(
        this LayoutListNode list,
        IReadOnlyList<T> dataList,
        Func<T, TKey> getKeyFromData,
        Func<TU, TKey> getKeyFromNode,
        Action<TU, T> updateNode,
        Func<T, TU> createNodeMethod,
        IEqualityComparer<TKey>? keyComparer = null) where TU : NodeBase where TKey : notnull {

        keyComparer ??= EqualityComparer<TKey>.Default;

        var desiredKeys = new HashSet<TKey>(keyComparer);
        foreach (var data in dataList) {
            desiredKeys.Add(getKeyFromData(data));
        }

        // Drop nodes that no longer have a matching data item.
        var obsolete = list.GetNodes<TU>().Where(node => !desiredKeys.Contains(getKeyFromNode(node))).ToList();
        var structureChanged = obsolete.Count > 0;
        foreach (var node in obsolete) {
            list.RemoveNode(node);
        }

        // Re-use the nodes that are still valid, creating the missing ones.
        var nodesByKey = new Dictionary<TKey, TU>(keyComparer);
        foreach (var node in list.GetNodes<TU>()) {
            var key = getKeyFromNode(node);
            if (!nodesByKey.ContainsKey(key)) {
                nodesByKey[key] = node;
            }
        }

        var createdNodes = new List<NodeBase>();
        foreach (var data in dataList) {
            var key = getKeyFromData(data);
            if (nodesByKey.TryGetValue(key, out var existing)) {
                updateNode(existing, data);
                continue;
            }

            var created = createNodeMethod(data);
            createdNodes.Add(created);
            nodesByKey[key] = created;
            structureChanged = true;
        }

        if (createdNodes.Count > 0) {
            // Adding the nodes one at a time recalculates the layout of the whole list for every single
            // node, which is quadratic in the number of entries.
            list.AddNode(createdNodes);
        }

        // Bring the nodes into the order of the data list. Nodes that do not hold data keep the slot they
        // occupied relative to their neighbouring data nodes, instead of being pushed to the end.
        var order = new Dictionary<TKey, int>(keyComparer);
        for (var i = 0; i < dataList.Count; i++) {
            order[getKeyFromData(dataList[i])] = i;
        }

        var slots = new Dictionary<NodeBase, double>();
        var previousSlot = -0.5;
        foreach (var node in list.Nodes) {
            if (node is TU typed) {
                var slot = order.TryGetValue(getKeyFromNode(typed), out var index) ? (double)index : double.MaxValue;
                slots[node] = slot;
                previousSlot = slot;
            }
            else {
                slots[node] = previousSlot + 0.5;
            }
        }

        bool orderChanged = false;
        if (list.GetNodes<TU>().Count() > 1) {
            var current = list.Nodes.Where(node => node is TU).ToList();
            var target = current.OrderBy(node => slots[node]).ToList();
            orderChanged = !current.SequenceEqual(target);

            if (orderChanged) {
                list.ReorderNodes((left, right) => slots[left].CompareTo(slots[right]));
            }
        }

        if (structureChanged || orderChanged) {
            list.RecalculateLayout();
        }

        return structureChanged || orderChanged;
    }
}
