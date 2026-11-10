using System;
using KamiToolKit.BaseTypes;

namespace AetherBags.Nodes.Layout;

/// <summary>
/// Layout constants shared between the inventory scrolling area and the windows that host it.
/// </summary>
public static class InventoryScrollingLayout {

    /// <summary>
    /// Width that is reserved for the scroll bar and its gutter.
    /// </summary>
    /// <remarks>
    /// The upstream scrolling node reserves the scroll bar thickness plus two pixels, which is narrower than
    /// the gutter the inventory layout uses. The inventory scrolling area owns the value so that the content
    /// width does not depend on the order in which the sizes are applied.
    /// </remarks>
    public const float ScrollBarWidth = 16.0f;
}

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

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        // Own the content width here instead of letting the callers patch it afterwards, so that the grid
        // keeps its width no matter which code path resizes the scrolling area.
        ContentNode.Width = Math.Max(0.0f, Width - InventoryScrollingLayout.ScrollBarWidth);
    }
}
