namespace Una.Drawing;

internal static partial class Layout
{
    public static void ComputeBounds(Node node)
    {
        if (node.ParentNode != null) {
            node = node.RootNode;
        }

        if (node.IsDisposed || !node.ComputedStyle.IsVisible) return;

        var lastWidth = node.Bounds.MarginSize.Width;
        var lastHeight = node.Bounds.MarginSize.Height;

        // Sizes can depend on each other (auto-size, text wrapping, max size),
        // which may oscillate forever. Bail out instead of hanging the frame.
        const int maxIterations = 8;

        for (var iteration = 0; iteration < maxIterations; iteration++) {
            ComputeSizes(node);

            var newWidth = node.Bounds.MarginSize.Width;
            var newHeight = node.Bounds.MarginSize.Height;

            if (Math.Abs(lastWidth - newWidth) < 0.1 && Math.Abs(lastHeight - newHeight) < 0.1) return;

            lastWidth  = newWidth;
            lastHeight = newHeight;
        }
    }
    
    public static void ComputeLayout(Node node, Vector2 origin)
    {
        origin = ComputeAnchorPoint(node, origin);

        node.Bounds.MarginRect = new Rect(origin, node.Bounds.MarginSize);
        
        node.Bounds.PaddingRect = new Rect(
            (int)Math.Ceiling(origin.X) + node.ComputedStyle.Margin.Left,
            (int)Math.Ceiling(origin.Y) + node.ComputedStyle.Margin.Top,
            node.Bounds.PaddingSize
        );
        
        node.Bounds.ContentRect = new Rect(
            (int)Math.Ceiling(origin.X) + node.ComputedStyle.Padding.Left,
            (int)Math.Ceiling(origin.Y) + node.ComputedStyle.Padding.Top,
            node.Bounds.ContentSize
        );

        ComputePositions(node);
    }

    public static void OverridePositionsOf(Node node, Vector2 pos)
    {
        pos = ComputeAnchorPoint(node, pos);
        
        node.Bounds.MarginRect = new Rect(pos, node.Bounds.MarginSize);
        
        node.Bounds.PaddingRect = new Rect(
            (int)Math.Ceiling(pos.X) + node.ComputedStyle.Margin.Left,
            (int)Math.Ceiling(pos.Y) + node.ComputedStyle.Margin.Top,
            node.Bounds.PaddingSize
        );
        
        node.Bounds.ContentRect = new Rect(
            (int)Math.Ceiling(pos.X) + node.ComputedStyle.Margin.Left + node.ComputedStyle.Padding.Left,
            (int)Math.Ceiling(pos.Y) + node.ComputedStyle.Margin.Top + node.ComputedStyle.Padding.Top,
            node.Bounds.ContentSize
        );
        
        ComputePositions(node);
    }

    private static Vector2 ComputeAnchorPoint(Node node, Vector2 origin)
    {
        Size   size   = node.Bounds.MarginSize;
        Anchor anchor = node.ComputedStyle.Anchor;

        float x = origin.X;
        float y = origin.Y;
     
        // X-axis.
        if (anchor.IsCenter) {
            x -= size.Width / 2f;
        } else if (anchor.IsRight) {
            x -= size.Width;
        }
        
        // Y-axis.
        if (anchor.IsMiddle) {
            y -= size.Height / 2f;
        } else if (anchor.IsBottom) {
            y -= size.Height;
        }

        return new(x, y);
    }
}