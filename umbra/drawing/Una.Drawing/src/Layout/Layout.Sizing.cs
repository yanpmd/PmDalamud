using System.Collections.Immutable;
using System.Linq;

namespace Una.Drawing;

internal static partial class Layout
{
    /// <summary>
    /// Computes the sizes of the given node and its children.
    /// </summary>
    private static void ComputeSizes(Node node)
    {
        ComputeFixedAndFitSizes(node);
        if (!ComputeGrowingSizes(node)) {
            return; // No need to stabilize.
        }

        // Second pass to stabilize for text wrapping & overflow.
        ComputeFixedAndFitSizes(node);
        ComputeGrowingSizes(node);
    }

    private static void ComputeFixedAndFitSizes(Node node)
    {
        bool isHorizontal = node.ComputedStyle.Flow == Flow.Horizontal;

        // Use clearer names reflecting the final calculated dimension
        float childrenCalculatedWidth;
        float childrenCalculatedHeight;

        // Use temporary accumulators based on flow direction
        float mainAxisAccumulatedSize = 0;
        float crossAxisMaxSize        = 0;

        bool isAutoWidth  = node.ComputedStyle.Size.Width <= 0;
        bool isAutoHeight = node.ComputedStyle.Size.Height <= 0;

        float visibleChildCount = 0; // Needed for correct gap calculation

        foreach (Node child in node.ChildNodes.ToArray()) {
            if (child.IsDisposed || !child.ComputedStyle.IsVisible) continue;

            ComputeFixedAndFitSizes(child);

            if (child.ComputedStyle.Anchor == Anchor.AnchorPoint.None) continue;
            visibleChildCount++;

            float childOuterWidth  = child.OuterWidth;
            float childOuterHeight = child.OuterHeight;

            if (isHorizontal) {
                mainAxisAccumulatedSize += childOuterWidth;                              // Sum widths
                crossAxisMaxSize        =  MathF.Max(crossAxisMaxSize, childOuterHeight); // Max height
            } else {
                mainAxisAccumulatedSize += childOuterHeight;                            // Sum heights
                crossAxisMaxSize        =  MathF.Max(crossAxisMaxSize, childOuterWidth); // Max width
            }
        }

        mainAxisAccumulatedSize += (node.ComputedStyle.Gap > 0 && visibleChildCount > 1)
            ? (visibleChildCount - 1) * node.ComputedStyle.Gap
            : 0;

        if (isHorizontal) {
            childrenCalculatedWidth  = mainAxisAccumulatedSize; // Sum of Widths + Gaps from children.
            childrenCalculatedHeight = crossAxisMaxSize;        // Max Height from children.
        } else {
            childrenCalculatedWidth  = crossAxisMaxSize;        // Max Width from children.
            childrenCalculatedHeight = mainAxisAccumulatedSize; // Sum of Heights + Gaps from children.
        }

        Size textSize = node.ComputeContentSizeFromText();

        float outerW = node.ComputedStyle.Padding.HorizontalSize + node.ComputedStyle.Margin.HorizontalSize;
        float outerH = node.ComputedStyle.Padding.VerticalSize + node.ComputedStyle.Margin.VerticalSize;

        var finalContentWidth = isAutoWidth
            ? MathF.Max(childrenCalculatedWidth, textSize.Width + 2)
            : node.ComputedStyle.Size.Width - outerW;

        var finalContentHeight = isAutoHeight
            ? MathF.Max(childrenCalculatedHeight, textSize.Height)
            : node.ComputedStyle.Size.Height - outerH;

        if (node.ComputedStyle.MaxWidth is > 0) {
            finalContentWidth = MathF.Min(finalContentWidth, node.ComputedStyle.MaxWidth.Value);
        }

        node.Bounds.ContentSize = new Size(
            MathF.Max(0, finalContentWidth),
            MathF.Max(0, finalContentHeight)
        );

        node.Bounds.PaddingSize = new Size(
            node.Bounds.ContentSize.Width + node.ComputedStyle.Padding.HorizontalSize,
            node.Bounds.ContentSize.Height + node.ComputedStyle.Padding.VerticalSize
        );

        node.Bounds.MarginSize = new Size(
            node.Bounds.PaddingSize.Width + node.ComputedStyle.Margin.HorizontalSize,
            node.Bounds.PaddingSize.Height + node.ComputedStyle.Margin.VerticalSize
        );
    }

    private static bool ComputeGrowingSizes(Node node)
    {
        Flow axis          = node.ComputedStyle.Flow;
        Flow crossAxis     = axis == Flow.Horizontal ? Flow.Vertical : Flow.Horizontal;
        bool mustStabilize = false;

        foreach (List<Node> children in node.AnchorToChildNodes.Values.ToArray()) {
            GrowChildrenAlongAxis(node, axis, children);
            GrowChildrenAlongCrossAxis(node, crossAxis, children);
        }

        foreach (Node child in node.ChildNodes.ToArray()) {
            if (ComputeGrowingSizes(child)) {
                mustStabilize = true;
            }
        }

        // Reset and recompute text size after layout if necessary.
        if (node.ComputedStyle.WordWrap || !node.ComputedStyle.TextOverflow) {
            node.ClearTextCache();
            Size newTextSize = node.ComputeContentSizeFromText();

            if ((node.ComputedStyle.AutoSize.Horizontal == AutoSize.Fit && node.ComputedStyle.Size.Width == 0 &&
                 Math.Abs(newTextSize.Width - node.Bounds.ContentSize.Width) > 0.9f) ||
                (node.ComputedStyle.AutoSize.Vertical == AutoSize.Fit && node.ComputedStyle.Size.Height == 0 &&
                 Math.Abs(newTextSize.Height - node.Bounds.ContentSize.Height) > 0.9f)
            ) {
                return true;
            }
        }

        return mustStabilize;
    }

    private static void GrowChildrenAlongAxis(Node node, Flow axis, List<Node> children)
    {
        List<Node>                 growableChildren;
        Func<Node, float>          getOuterSize;
        Func<ComputedStyle, float> getPaddingSize;
        Func<ComputedStyle, float> getMarginSize;
        float                      parentContentSize;
        float                      gap = node.ComputedStyle.Gap;

        // Configure based on axis.
        if (axis == Flow.Horizontal) {
            // Build growable children list without LINQ to avoid allocations.
            growableChildren = new List<Node>();
            foreach (var n in children) {
                if (n is { IsDisposed: false, ComputedStyle: { AutoSize.Horizontal: AutoSize.Grow, IsVisible: true } }) {
                    growableChildren.Add(n);
                }
            }
            getOuterSize      = n => n.OuterWidth;
            getPaddingSize    = cs => cs.Padding.HorizontalSize;
            getMarginSize     = cs => cs.Margin.HorizontalSize;
            parentContentSize = node.Bounds.ContentSize.Width;
        } else {
            // Build growable children list without LINQ to avoid allocations.
            growableChildren = new List<Node>();
            foreach (var n in children) {
                if (n is { IsDisposed: false, ComputedStyle: { AutoSize.Vertical: AutoSize.Grow, IsVisible: true } }) {
                    growableChildren.Add(n);
                }
            }
            getOuterSize      = n => n.OuterHeight;
            getPaddingSize    = cs => cs.Padding.VerticalSize;
            getMarginSize     = cs => cs.Margin.VerticalSize;
            parentContentSize = node.Bounds.ContentSize.Height;
        }

        // Abort if there is nothing to grow.
        if (growableChildren.Count == 0) return;

        // Calculate space used by non-growable items and gaps without LINQ.
        var growableSet = new System.Collections.Generic.HashSet<Node>(growableChildren);
        float nonGrowableSize = 0f;
        int visibleChildCount = 0;
        foreach (var n in children) {
            if (n is { IsDisposed: false, ComputedStyle.IsVisible: true }) {
                if (!growableSet.Contains(n)) {
                    nonGrowableSize += getOuterSize(n);
                }
                if (n.ComputedStyle.Anchor != Anchor.AnchorPoint.None) visibleChildCount++;
            }
        }

        if (growableChildren.Count == 0) return;

        // Calculate space used by non-growable items and gaps.
        nonGrowableSize = children
                               .Where(n => !growableChildren.Contains(n) && n is { IsDisposed: false, ComputedStyle.IsVisible: true })
                               .Sum(n => getOuterSize(n));

        visibleChildCount = children.Count(n => n is { IsDisposed: false, ComputedStyle.IsVisible: true } && n.ComputedStyle.Anchor != Anchor.AnchorPoint.None);
        float totalGapSize      = children.Count > 1 ? gap * (visibleChildCount - 1) : 0;

        // Total space the growable items should collectively occupy.
        float availableSizeForGrowableGroup = parentContentSize - nonGrowableSize - totalGapSize;
        availableSizeForGrowableGroup = MathF.Max(0, availableSizeForGrowableGroup);

        // Distribute available size.
        float baseTargetOuterSize = availableSizeForGrowableGroup / growableChildren.Count;
        float remainderSize       = availableSizeForGrowableGroup % growableChildren.Count;

        foreach (Node child in growableChildren) {
            if (child.IsDisposed || !child.ComputedStyle.IsVisible) continue;
            if (child.ComputedStyle.Anchor.IsNone) {
                float to   = availableSizeForGrowableGroup;
                float cps  = getPaddingSize(child.ComputedStyle);
                float cms  = getMarginSize(child.ComputedStyle);
                float from = to - (cps + cms);

                if (from < 0) from = 0;

                child.Bounds.ContentSize = axis == Flow.Horizontal
                    ? new Size(from, child.Bounds.ContentSize.Height)
                    : new Size(child.Bounds.ContentSize.Width, from);

                child.Bounds.PaddingSize = child.Bounds.ContentSize + child.ComputedStyle.Padding.Size;
                child.Bounds.MarginSize  = child.Bounds.PaddingSize + child.ComputedStyle.Margin.Size;
                continue;
            }

            float targetOuterSize  = baseTargetOuterSize + (remainderSize > 0 ? 1 : 0);
            float childPaddingSize = getPaddingSize(child.ComputedStyle);
            float childMarginSize  = getMarginSize(child.ComputedStyle);
            float newContentSize   = targetOuterSize - (childPaddingSize + childMarginSize);

            newContentSize = MathF.Max(0, newContentSize);

            child.Bounds.ContentSize = axis == Flow.Horizontal
                ? new Size(newContentSize, child.Bounds.ContentSize.Height)
                : new Size(child.Bounds.ContentSize.Width, newContentSize);

            child.Bounds.PaddingSize = child.Bounds.ContentSize + child.ComputedStyle.Padding.Size;
            child.Bounds.MarginSize  = child.Bounds.PaddingSize + child.ComputedStyle.Margin.Size;

            if (remainderSize > 0) { remainderSize--; }
        }
    }

    private static void GrowChildrenAlongCrossAxis(Node node, Flow axisToStretch, List<Node> children)
    {
        float parentContentSize = (axisToStretch == Flow.Horizontal)
            ? node.Bounds.ContentSize.Width
            : node.Bounds.ContentSize.Height;

        Func<Node, bool>           shouldStretchChild;
        Func<ComputedStyle, float> getPaddingSize;
        Func<ComputedStyle, float> getMarginSize;

        if (axisToStretch == Flow.Horizontal) {
            shouldStretchChild = n =>
                n.ComputedStyle.AutoSize.Horizontal is AutoSize.Grow && n.ComputedStyle.Size.Width <= 0;
            getPaddingSize = cs => cs.Padding.HorizontalSize;
            getMarginSize  = cs => cs.Margin.HorizontalSize;
        } else {
            shouldStretchChild = n =>
                n.ComputedStyle.AutoSize.Vertical is AutoSize.Grow && n.ComputedStyle.Size.Height <= 0;
            getPaddingSize = cs => cs.Padding.VerticalSize;
            getMarginSize  = cs => cs.Margin.VerticalSize;
        }

        foreach (Node child in children) {
            if (child.IsDisposed || !child.ComputedStyle.IsVisible) continue;
            if (!shouldStretchChild(child)) continue;

            float childPaddingSize = getPaddingSize(child.ComputedStyle);
            float childMarginSize  = getMarginSize(child.ComputedStyle);
            float newContentSize   = parentContentSize - (childPaddingSize + childMarginSize);

            newContentSize = MathF.Max(0, newContentSize);

            // Get current content size on the axis to potentially update.
            float currentContentSize = (axisToStretch == Flow.Horizontal)
                ? child.Bounds.ContentSize.Width
                : child.Bounds.ContentSize.Height;

            // Abort if the content size is already the same.
            if (Math.Abs(newContentSize - currentContentSize) < 0.1f) continue;

            child.Bounds.ContentSize = axisToStretch == Flow.Horizontal
                ? new Size(newContentSize, child.Bounds.ContentSize.Height)
                : new Size(child.Bounds.ContentSize.Width, newContentSize);

            child.Bounds.PaddingSize = child.Bounds.ContentSize + child.ComputedStyle.Padding.Size;
            child.Bounds.MarginSize  = child.Bounds.PaddingSize + child.ComputedStyle.Margin.Size;
        }
    }
}