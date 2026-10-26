namespace Una.Drawing.Generator;

internal class DropShadowGenerator : IGenerator
{
    public int RenderOrder => -1;

    public bool Generate(SKCanvas canvas, Node node, Vector2 origin)
    {
        if (node.IsDisposed || !node.IsVisible) return false;
        if (node.ComputedStyle.DropShadow == Vector4.Zero) return false;

        SKColor color = new(0, 0, 0, 255);

        using SKPaint paint = new();

        paint.Color = new SKColor(0, 0, 0, 255);
        paint.ImageFilter = SKImageFilter.CreateDropShadow(
            node.ComputedStyle.DropShadow.X,
            node.ComputedStyle.DropShadow.Y,
            node.ComputedStyle.DropShadow.Z,
            node.ComputedStyle.DropShadow.W,
            color
        );

        Size  size = node.Bounds.PaddingSize;
        float rc   = node.ComputedStyle.BorderRadius;

        canvas.DrawRect(origin.X + rc, origin.Y + rc, size.Width - (rc * 2f), size.Height - (rc * 2f), paint);
        paint.ImageFilter?.Dispose();

        return true;
    }
}