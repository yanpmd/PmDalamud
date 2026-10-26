namespace Una.Drawing;

[StructLayout(LayoutKind.Sequential)]
internal struct LayoutStyleSnapshot
{
    internal bool                 IsVisible;
    internal Flow                 Flow;
    internal FlowOrder            FlowOrder;
    internal Anchor.AnchorPoint   Anchor;
    internal (AutoSize, AutoSize) AutoSize;
    internal float                Width;
    internal float                Height;
    internal float                Gap;
    internal float                PaddingTop;
    internal float                PaddingRight;
    internal float                PaddingBottom;
    internal float                PaddingLeft;
    internal float                MarginTop;
    internal float                MarginRight;
    internal float                MarginBottom;
    internal float                MarginLeft;
    internal bool                 WordWrap;
    internal uint                 Font;
    internal int                  FontSize;
    internal float                LineHeight;
    internal bool                 TextOverflow;
    internal float?               MaxWidth;
    

    internal static LayoutStyleSnapshot Create(ref ComputedStyle style)
    {
        return new() {
            Anchor        = style.Anchor.Point,
            IsVisible     = style.IsVisible,
            Width         = style.Size.Width,
            Height        = style.Size.Height,
            Flow          = style.Flow,
            FlowOrder     = style.FlowOrder,
            Gap           = style.Gap,
            PaddingTop    = style.Padding.Top,
            PaddingRight  = style.Padding.Right,
            PaddingBottom = style.Padding.Bottom,
            PaddingLeft   = style.Padding.Left,
            MarginTop     = style.Margin.Top,
            MarginRight   = style.Margin.Right,
            MarginBottom  = style.Margin.Bottom,
            MarginLeft    = style.Margin.Left,
            WordWrap      = style.WordWrap,
            Font          = style.Font,
            FontSize      = style.FontSize,
            LineHeight    = style.LineHeight,
            TextOverflow  = style.TextOverflow,
            MaxWidth      = style.MaxWidth,
            AutoSize      = style.AutoSize,
        };
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.AddBytes(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this), 1)));

        return hash.ToHashCode();
    }
}