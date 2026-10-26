namespace Una.Drawing.Generator;

internal class BackgroundGenerator : IGenerator
{
    /// <inheritdoc/>
    public int RenderOrder => 0;

    /// <inheritdoc/>
    public bool Generate(SKCanvas canvas, Node node, Vector2 origin)
    {
        if (null == node.ComputedStyle.BackgroundColor) return false;

        using var paint = new SKPaint();

        Size size = node.Bounds.PaddingSize;

        paint.Color       = Color.ToSkColor(node.ComputedStyle.BackgroundColor);
        paint.Style       = SKPaintStyle.Fill;

        if (node.ComputedStyle.BorderRadius == 0) {
            canvas.DrawRect(origin.X, origin.Y, size.Width, size.Height, paint);
            return true;
        }

        paint.IsAntialias = node.ComputedStyle.IsAntialiased;

        var style  = node.ComputedStyle;
        var radius = (float)style.BorderRadius;

        RoundedCorners corners     = style.RoundedCorners;
        SKRect         rect        = new(origin.X, origin.Y, size.Width + origin.X, size.Height + origin.Y);
        SKPoint        topLeft     = corners.HasFlag(RoundedCorners.TopLeft) ? new(radius, radius) : new(0, 0);
        SKPoint        topRight    = corners.HasFlag(RoundedCorners.TopRight) ? new(radius, radius) : new(0, 0);
        SKPoint        bottomRight = corners.HasFlag(RoundedCorners.BottomRight) ? new(radius, radius) : new(0, 0);
        SKPoint        bottomLeft  = corners.HasFlag(RoundedCorners.BottomLeft) ? new(radius, radius) : new(0, 0);

        using SKRoundRect roundRect = new SKRoundRect(rect, radius, radius);

        roundRect.SetRectRadii(rect, [topLeft, topRight, bottomRight, bottomLeft]);
        canvas.DrawRoundRect(roundRect, paint);

        return true;
    }
}