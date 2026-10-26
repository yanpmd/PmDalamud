namespace Una.Drawing;

internal static class ComputedStyleFactory
{
    internal static (int, ComputedStyle) Create(Node node)
    {
        if (node.IsDisposed) return (0, new());

        var computedStyle = CreateDefault();
        var hashCode      = new HashCode();

        if (node.Stylesheet is not null) {
            List<Stylesheet.Rule> matchingRules = [];

            foreach (var rule in node.Stylesheet.Rules.Keys) {
                if (rule.Matches(node)) {
                    matchingRules.Add(rule);
                }
            }

            matchingRules.Sort((a, b) => {
                int specificityComparison = a.Specificity.CompareTo(b.Specificity);

                return specificityComparison != 0
                    ? specificityComparison
                    : a.SourceOrderIndex.CompareTo(b.SourceOrderIndex);
            });

            foreach (var rule in matchingRules) {
                Apply(ref computedStyle, node.Stylesheet.Rules[rule]);
                hashCode.Add(rule.ToString());
            }
        }

        Apply(ref computedStyle, node.Style);
        ApplyScaleFactor(ref computedStyle);

        hashCode.Add(node.Style);

        return (hashCode.ToHashCode(), computedStyle);
    }

    /// <summary>
    /// Applies the configured rules in the given <see cref="Style"/> object to
    /// the specified <see cref="ComputedStyle"/> object.
    /// </summary>
    private static void Apply(ref ComputedStyle cs, in Style style)
    {
        cs.IsVisible                 = style.IsVisible ?? cs.IsVisible;
        cs.TransitionDuration        = style.TransitionDuration ?? cs.TransitionDuration;
        cs.TransitionType            = style.TransitionType ?? cs.TransitionType;
        cs.TransitionAddClass        = style.TransitionAddClass ?? cs.TransitionAddClass;
        cs.TransitionRemoveClass     = style.TransitionRemoveClass ?? cs.TransitionRemoveClass;
        cs.Anchor                    = style.Anchor ?? cs.Anchor;
        cs.Size                      = style.Size ?? cs.Size;
        cs.AutoSize                  = style.AutoSize ?? cs.AutoSize;
        cs.Flow                      = style.Flow ?? cs.Flow;
        cs.FlowOrder                 = style.FlowOrder ?? cs.FlowOrder;
        cs.Gap                       = style.Gap ?? cs.Gap;
        cs.Padding                   = style.Padding ?? cs.Padding;
        cs.Margin                    = style.Margin ?? cs.Margin;
        cs.Color                     = style.Color ?? cs.Color;
        cs.Font                      = style.Font ?? cs.Font;
        cs.FontSize                  = style.FontSize ?? cs.FontSize;
        cs.LineHeight                = style.LineHeight ?? cs.LineHeight;
        cs.WordWrap                  = style.WordWrap ?? cs.WordWrap;
        cs.TextAlign                 = style.TextAlign ?? cs.TextAlign;
        cs.OutlineSize               = style.OutlineSize ?? cs.OutlineSize;
        cs.TextOffset                = style.TextOffset ?? cs.TextOffset;
        cs.TextOverflow              = style.TextOverflow ?? cs.TextOverflow;
        cs.MaxWidth                  = style.MaxWidth ?? cs.MaxWidth;
        cs.BackgroundColor           = style.BackgroundColor ?? cs.BackgroundColor;
        cs.BorderColor               = style.BorderColor ?? cs.BorderColor;
        cs.BorderInset               = style.BorderInset ?? cs.BorderInset;
        cs.BorderRadius              = style.BorderRadius ?? cs.BorderRadius;
        cs.BorderWidth               = style.BorderWidth ?? cs.BorderWidth;
        cs.StrokeColor               = style.StrokeColor ?? cs.StrokeColor;
        cs.StrokeWidth               = style.StrokeWidth ?? cs.StrokeWidth;
        cs.StrokeInset               = style.StrokeInset ?? cs.StrokeInset;
        cs.StrokeRadius              = style.StrokeRadius ?? cs.StrokeRadius;
        cs.RoundedCorners            = style.RoundedCorners ?? cs.RoundedCorners;
        cs.BackgroundGradient        = style.BackgroundGradient ?? cs.BackgroundGradient;
        cs.BackgroundGradientInset   = style.BackgroundGradientInset ?? cs.BackgroundGradientInset;
        cs.BackgroundImage           = style.BackgroundImage ?? cs.BackgroundImage;
        cs.BackgroundImageInset      = style.BackgroundImageInset ?? cs.BackgroundImageInset;
        cs.BackgroundImageScale      = style.BackgroundImageScale ?? cs.BackgroundImageScale;
        cs.BackgroundImageColor      = style.BackgroundImageColor ?? cs.BackgroundImageColor;
        cs.BackgroundImageRotation   = style.BackgroundImageRotation ?? cs.BackgroundImageRotation;
        cs.BackgroundImageBlendMode  = style.BackgroundImageBlendMode ?? cs.BackgroundImageBlendMode;
        cs.OutlineColor              = style.OutlineColor ?? cs.OutlineColor;
        cs.TextShadowSize            = style.TextShadowSize ?? cs.TextShadowSize;
        cs.TextShadowColor           = style.TextShadowColor ?? cs.TextShadowColor;
        cs.DropShadow                = style.DropShadow ?? cs.DropShadow;
        cs.BitmapFontIcon            = style.BitmapFontIcon ?? cs.BitmapFontIcon;
        cs.IconId                    = style.IconId ?? cs.IconId;
        cs.ImageBytes                = style.ImageBytes ?? cs.ImageBytes;
        cs.ImageInset                = style.ImageInset ?? cs.ImageInset;
        cs.ImageOffset               = style.ImageOffset ?? cs.ImageOffset;
        cs.ImageRounding             = style.ImageRounding ?? cs.ImageRounding;
        cs.ImageRoundedCorners       = style.ImageRoundedCorners ?? cs.ImageRoundedCorners;
        cs.ImageScaleMode            = style.ImageScaleMode ?? cs.ImageScaleMode;
        cs.ImageTileMode             = style.ImageTileMode ?? cs.ImageTileMode;
        cs.ImageScale                = style.ImageScale ?? cs.ImageScale;
        cs.ImageBlur                 = style.ImageBlur ?? cs.ImageBlur;
        cs.ImageGrayscale            = style.ImageGrayscale ?? cs.ImageGrayscale;
        cs.ImageContrast             = style.ImageContrast ?? cs.ImageContrast;
        cs.ImageRotation             = style.ImageRotation ?? cs.ImageRotation;
        cs.ImageColor                = style.ImageColor ?? cs.ImageColor;
        cs.ImageBlendMode            = style.ImageBlendMode ?? cs.ImageBlendMode;
        cs.Opacity                   = style.Opacity ?? cs.Opacity;
        cs.ShadowSize                = style.ShadowSize ?? cs.ShadowSize;
        cs.ShadowInset               = style.ShadowInset ?? cs.ShadowInset;
        cs.ShadowOffset              = style.ShadowOffset ?? cs.ShadowOffset;
        cs.IsAntialiased             = style.IsAntialiased ?? cs.IsAntialiased;
        cs.ScrollbarTrackColor       = style.ScrollbarTrackColor ?? cs.ScrollbarTrackColor;
        cs.ScrollbarThumbColor       = style.ScrollbarThumbColor ?? cs.ScrollbarThumbColor;
        cs.ScrollbarThumbHoverColor  = style.ScrollbarThumbHoverColor ?? cs.ScrollbarThumbHoverColor;
        cs.ScrollbarThumbActiveColor = style.ScrollbarThumbActiveColor ?? cs.ScrollbarThumbActiveColor;
        cs.UldResource               = style.UldResource ?? cs.UldResource;
        cs.UldPartsId                = style.UldPartsId ?? cs.UldPartsId;
        cs.UldPartId                 = style.UldPartId ?? cs.UldPartId;
        cs.UldStyle                  = style.UldStyle ?? cs.UldStyle;
    }

    /// <summary>
    /// Applies the configured scale factor.
    /// </summary>
    private static void ApplyScaleFactor(ref ComputedStyle computedStyle)
    {
        computedStyle.Size    *= Node.ScaleFactor;
        computedStyle.Padding *= Node.ScaleFactor;

        computedStyle.MaxWidth =
            computedStyle.MaxWidth != null ? (int)(computedStyle.MaxWidth * Node.ScaleFactor) : null;

        computedStyle.Gap          =  (int)(computedStyle.Gap * Node.ScaleFactor);
        computedStyle.FontSize     =  (int)(computedStyle.FontSize * Node.ScaleFactor);
        computedStyle.TextOffset   *= Node.ScaleFactor;
        computedStyle.BorderInset  *= Node.ScaleFactor;
        computedStyle.OutlineSize  *= Node.ScaleFactor;
        computedStyle.BorderRadius =  (int)(computedStyle.BorderRadius * Node.ScaleFactor);

        computedStyle.BackgroundGradientInset *= Node.ScaleFactor;

        if (Node.ScaleAffectsBorders) {
            computedStyle.BorderWidth *= Node.ScaleFactor;
            computedStyle.StrokeWidth =  (int)MathF.Ceiling(computedStyle.StrokeWidth * Node.ScaleFactor);
        }

        computedStyle.StrokeInset    *= Node.ScaleFactor;
        computedStyle.StrokeRadius   *= Node.ScaleFactor;
        computedStyle.TextShadowSize *= Node.ScaleFactor;
        computedStyle.ImageInset     *= Node.ScaleFactor;
        computedStyle.ImageRounding  *= Node.ScaleFactor;
        computedStyle.ImageOffset    *= Node.ScaleFactor;
        computedStyle.ShadowSize     *= Node.ScaleFactor;
        computedStyle.ShadowOffset   *= Node.ScaleFactor;
        computedStyle.ShadowInset    =  (int)(computedStyle.ShadowInset * Node.ScaleFactor);
    }

    /// <summary>
    /// Assigns default values to the given style object.
    /// </summary>
    internal static ComputedStyle CreateDefault()
    {
        return new ComputedStyle {
            IsVisible                 = true,
            Anchor                    = Anchor.TopLeft,
            Size                      = new(),
            Flow                      = Flow.Horizontal,
            FlowOrder                 = FlowOrder.Normal,
            Gap                       = 0,
            Padding                   = new(),
            Color                     = new(0xFFC0C0C0),
            Font                      = 0,
            FontSize                  = 12,
            LineHeight                = 1.2f,
            WordWrap                  = false,
            TextAlign                 = Anchor.TopLeft,
            OutlineColor              = null,
            OutlineSize               = 1,
            TextOffset                = Vector2.Zero,
            TextOverflow              = true,
            MaxWidth                  = null,
            BackgroundColor           = null,
            BorderColor               = null,
            BorderInset               = new(),
            BorderRadius              = 0,
            BorderWidth               = new(),
            StrokeColor               = null,
            StrokeWidth               = 0,
            StrokeInset               = 0,
            StrokeRadius              = null,
            RoundedCorners            = RoundedCorners.All,
            BackgroundGradient        = null,
            BackgroundGradientInset   = new(),
            BackgroundImage           = null,
            BackgroundImageInset      = new(0, 0),
            BackgroundImageScale      = new(1, 1),
            BackgroundImageColor      = new(0xFFFFFFFF),
            BackgroundImageBlendMode  = BlendMode.Modulate,
            TextShadowSize            = 0,
            TextShadowColor           = null,
            DropShadow                = Vector4.Zero,
            BitmapFontIcon            = null,
            IconId                    = null,
            ImageBytes                = null,
            ImageInset                = null,
            ImageOffset               = null,
            ImageRounding             = 0,
            ImageRoundedCorners       = RoundedCorners.All,
            ImageGrayscale            = false,
            ImageContrast             = 0,
            ImageRotation             = 0,
            ImageColor                = new(0xFFFFFFFF),
            ImageBlendMode            = BlendMode.Modulate,
            ImageTileMode             = ImageTileMode.Decal,
            ImageScaleMode            = ImageScaleMode.Adapt,
            ImageScale                = 1.0f,
            ImageBlur                 = Vector2.Zero,
            UldResource               = null,
            UldPartId                 = null,
            UldPartsId                = null,
            UldStyle                  = UldStyle.Default,
            Opacity                   = 1,
            ShadowSize                = new(),
            ShadowInset               = 0,
            ShadowOffset              = Vector2.Zero,
            IsAntialiased             = true,
            ScrollbarTrackColor       = new(0xFF404040),
            ScrollbarThumbColor       = new(0xFFA0A0A0),
            ScrollbarThumbHoverColor  = new(0xFFC0C0C0),
            ScrollbarThumbActiveColor = new(0xFFFFFFFF),
        };
    }
}