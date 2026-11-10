using System.Linq;
using KamiToolKit.BaseTypes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace AetherBags.Compat;

/// <summary>
/// Compatibility shim for KamiToolKit's removed <c>ScrollingAreaNode</c>.
/// <para>
/// Upstream replaced this node with <see cref="ScrollingNode{T}"/>, which behaves equivalently but exposes
/// different member names. This shim keeps the original API surface so existing call sites stay unchanged.
/// </para>
/// </summary>
/// <typeparam name="T">Type of the scrolled content.</typeparam>
public class ScrollingAreaNode<T> : ScrollingNode<T> where T : NodeBase, new() {

    /// <summary>
    /// Gets the node used to clip the contents of this scrolling area.
    /// </summary>
    public ResNode ContentAreaClipNode => ClippingContentNode;

    /// <summary>
    /// Gets the content node of this scrolling area.
    /// </summary>
    public T ContentAreaNode => ContentNode;

    /// <summary>
    /// Gets or sets the current scroll position.
    /// </summary>
    public int ScrollPosition {
        get => (int)ScrollBarNode.ScrollPosition;
        set => ScrollBarNode.ScrollPosition = value;
    }

    /// <summary>
    /// Gets or sets the height of the content area.
    /// </summary>
    public required float ContentHeight {
        get => ContentNode.Height;
        set {
            ContentNode.Height = value;
            ScrollBarNode.UpdateScrollParams();
        }
    }

    /// <summary>
    /// Sizes the content area to fit all of its children.
    /// </summary>
    public void FitToContentHeight() {
        if (ContentNode is not ILayoutListNode layoutNode) return;

        // Mirrors the height calculation of the list itself: the spacing is applied between two items,
        // so it must not be counted for the last one.
        ContentHeight = layoutNode.Nodes.Sum(node => node.IsVisible ? node.Height + layoutNode.ItemSpacing : 0.0f)
                          + layoutNode.FirstItemSpacing
                          - layoutNode.ItemSpacing;
    }
}
