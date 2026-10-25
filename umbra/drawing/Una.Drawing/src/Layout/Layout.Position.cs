using System.Collections.Immutable;
using System.Linq;

namespace Una.Drawing;

internal static partial class Layout
{
    private static void ComputePositions(Node root)
    {
        foreach (var anchorPoint in root.AnchorToChildNodes.Keys.ToArray()
                                        .Where(anchorPoint => root.AnchorToChildNodes[anchorPoint].Count > 0)
        ) {
            PositionNodesWithSameAnchor(anchorPoint, root, root.AnchorToChildNodes[anchorPoint]);
        }
    }

    private static void PositionNodesWithSameAnchor(Anchor anchor, Node root, List<Node> children)
    {
        int count = children.Count;
        if (count == 0) return;

        FlowOrder order = root.ComputedStyle.FlowOrder;
        Flow      flow  = root.ComputedStyle.Flow;

        int firstIndex = (order == FlowOrder.Normal) ? 0 : count - 1;
        int lastIndex  = (order == FlowOrder.Normal) ? count - 1 : 0;
        int step       = (order == FlowOrder.Normal) ? 1 : -1;

        bool firstFound = false;
        while ((step == 1 && firstIndex < count) || (step == -1 && firstIndex >= 0)) {
            if (children[firstIndex].ComputedStyle.IsVisible) {
                firstFound = true;
                break;
            }

            firstIndex += step;
        }

        if (!firstFound) return;

        while ((step == 1 && lastIndex >= firstIndex) || (step == -1 && lastIndex <= firstIndex)) {
            if (lastIndex < 0 || lastIndex >= count) break;
            if (children[lastIndex].ComputedStyle.IsVisible) break;

            lastIndex -= step;
        }

        (float originX, float originY) = GetNodeOrigin(anchor, root, children);

        float gap = root.ComputedStyle.Gap;
        float x   = originX, y = originY;

        float center = 0f;
        float middle = 0f;
        float right  = 0;

        if (flow == Flow.Horizontal) {
            if (anchor.IsCenter) {
                center = GetCenter(root);
                x      = center - (GetChildrenWidth(root, children) / 2f);
            }
        } else {
            if (anchor.IsCenter) {
                center = GetCenter(root);
                x      = center - (children[firstIndex].OuterWidth / 2f);
            } else if (anchor.IsRight) {
                right = GetChildrenWidth(root, children);
                x     = (originX + right) - children[firstIndex].OuterWidth;
            } else if (anchor.IsLeft) {
                x = originX;
            }
        }

        if (anchor.IsTop) {
            y = originY;
        } else if (anchor.IsBottom) {
            if (flow == Flow.Vertical) {
                y = originY - GetChildrenHeight(root, children);
            } else {
                y = originY - children[firstIndex].OuterHeight;
            }
        } else if (anchor.IsMiddle) {
            middle = GetMiddle(root);
            if (flow == Flow.Horizontal) {
                y = middle - (children[firstIndex].OuterHeight / 2f);
            } else {
                y = middle - (GetChildrenHeight(root, children) / 2f);
            }
        }

        int i = firstIndex;
        while (true) {
            // Boundary check: Stop if we've gone past the end index for the current direction.
            if ((step == 1 && i > lastIndex) || (step == -1 && i < lastIndex)) break;

            Node node = children[i];

            if (node.IsDisposed || !node.IsVisible) {
                i += step;
                continue;
            }

            x = MathF.Floor(x);
            y = MathF.Floor(y);

            node.Bounds.MarginRect = new Rect(x, y, node.Bounds.MarginSize);
            node.Bounds.PaddingRect = new Rect(
                x + node.ComputedStyle.Margin.Left,
                y + node.ComputedStyle.Margin.Top,
                node.Bounds.PaddingSize
            );

            node.Bounds.ContentRect = new Rect(
                x + node.ComputedStyle.Margin.Left + node.ComputedStyle.Padding.Left,
                y + node.ComputedStyle.Margin.Top + node.ComputedStyle.Padding.Top,
                node.Bounds.ContentSize
            );

            if (i == lastIndex) {
                ComputePositions(node);
                break;
            }

            Node nextNode = children[i + step];
            while (!nextNode.ComputedStyle.IsVisible || i == lastIndex) {
                i += step;
                if (i == lastIndex) break;
                nextNode = children[i + step];
            }

            switch (flow) {
                case Flow.Vertical:
                    // X-axis
                    if (anchor.IsLeft) {
                        x = originX;
                    } else if (anchor.IsCenter) {
                        x = center - (nextNode.OuterWidth / 2f);
                    } else if (anchor.IsRight) {
                        x = (originX + right) - nextNode.OuterWidth;
                    }

                    // Y-axis
                    if (!anchor.IsNone) y += node.OuterHeight + gap;
                    break;
                case Flow.Horizontal:
                default:
                    // X-axis
                    if (!anchor.IsNone) x += node.OuterWidth + gap;

                    // Y-axis
                    if (anchor.IsTop) {
                        y = originY;
                    } else if (anchor.IsMiddle) {
                        y = middle - (nextNode.OuterHeight / 2f);
                    } else if (anchor.IsBottom) {
                        y = originY - nextNode.OuterHeight;
                    }

                    break;
            }

            ComputePositions(node);
            i += step;
        }
    }

    private static (float, float) GetNodeOrigin(Anchor anchor, Node root, List<Node> children)
    {
        if (anchor.IsNone) {
            return (root.Bounds.ContentRect.X1, root.Bounds.ContentRect.Y1);
        }

        float x = 0;
        float y = 0;

        if (anchor.IsLeft) {
            x = root.Bounds.ContentRect.X1;
        } else if (anchor.IsRight) {
            x = root.Bounds.ContentRect.X2 - GetChildrenWidth(root, children);
        } else if (anchor.IsCenter) {
            x = GetCenterStart(root, children);
        }

        if (anchor.IsTop) {
            y = root.Bounds.ContentRect.Y1;
        } else if (anchor.IsBottom) {
            y = root.Bounds.ContentRect.Y2;
        } else if (anchor.IsMiddle) {
            y = GetMiddle(root);
        }

        return (x, y);
    }

    private static float GetCenterStart(Node root, List<Node> children)
    {
        return root.Bounds.ContentRect.X1 + Math.Abs(
            (int)Math.Ceiling((root.InnerWidth / 2f) - (GetChildrenWidth(root, children) / 2f)));
    }

    private static float GetCenter(Node root)
    {
        return root.Bounds.ContentRect.X1 + MathF.Ceiling(root.Bounds.ContentSize.Width / 2f);
    }

    private static float GetMiddle(Node root)
    {
        return root.Bounds.ContentRect.Y1 + MathF.Ceiling(root.Bounds.ContentSize.Height / 2f);
    }

    private static float GetChildrenWidth(Node root, List<Node> children)
    {
        var visibleChildren = new List<Node>();
        foreach (var _n in children)
            if (_n is { IsDisposed: false, ComputedStyle.IsVisible: true })
                visibleChildren.Add(_n);

        var enumerable = visibleChildren.ToArray();

        float width                           = 0f;
        foreach (var _n2 in enumerable) width += _n2.OuterWidth;

        if (root.ComputedStyle.Flow == Flow.Horizontal) {
            width += root.ComputedStyle.Gap > 0 ? (enumerable.Length - 1) * root.ComputedStyle.Gap : 0;
        }

        return width;
    }

    private static float GetChildrenHeight(Node root, List<Node> children)
    {
        var visibleChildren = new System.Collections.Generic.List<Node>();
        foreach (var _n in children)
            if (_n is { IsDisposed: false, ComputedStyle.IsVisible: true })
                visibleChildren.Add(_n);

        var enumerable = visibleChildren.ToArray();

        float height                           = 0f;
        foreach (var _n2 in enumerable) height += _n2.OuterHeight;

        if (root.ComputedStyle.Flow == Flow.Vertical) {
            height += root.ComputedStyle.Gap > 0 ? (enumerable.Length - 1) * root.ComputedStyle.Gap : 0;
        }

        return height;
    }
}