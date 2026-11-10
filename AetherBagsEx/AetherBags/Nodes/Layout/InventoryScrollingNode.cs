using KamiToolKit.BaseTypes;

namespace AetherBags.Nodes.Layout;

/// <summary>
/// A scrolling area used by the inventory UI.
/// <para>
/// This used to derive from KamiToolKit's (now removed) <c>ScrollingAreaNode</c>. Upstream replaced it with
/// <see cref="ScrollingNode{T}"/>, whose clipping content is a plain <see cref="ResNode"/> rather than a
/// component node. That resolves the original problem this class worked around: the drag-and-drop manager no
/// longer mistakes the clipping boundary for a drop target.
/// </para>
/// </summary>
/// <typeparam name="T">Type of the scrolled content.</typeparam>
public class InventoryScrollingAreaNode<T> : AetherBags.Compat.ScrollingAreaNode<T> where T : NodeBase, new() {
}
