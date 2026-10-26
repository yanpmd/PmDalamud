using Dalamud.Game.Text.SeStringHandling;
using Lumina.Misc;

namespace Una.Drawing;

[StructLayout(LayoutKind.Sequential)]
internal struct PaintStyleSnapshot
{
    internal Anchor.AnchorPoint TextAlign;
    internal float              OutlineSize;
    internal Vector2            TextOffset;
    internal uint               Color;
    internal uint?              BackgroundColor;
    internal uint?              BorderTopColor;
    internal uint?              BorderRightColor;
    internal uint?              BorderBottomColor;
    internal uint?              BorderLeftColor;
    internal float              BorderInsetTop;
    internal float              BorderInsetRight;
    internal float              BorderInsetBottom;
    internal float              BorderInsetLeft;
    internal float              BorderRadius;
    internal float              BorderTopWidth;
    internal float              BorderRightWidth;
    internal float              BorderBottomWidth;
    internal float              BorderLeftWidth;
    internal uint?              StrokeColor;
    internal float              StrokeWidth;
    internal float              StrokeInset;
    internal float?             StrokeRadius;
    internal RoundedCorners?    RoundedCorners;
    internal uint?              BackgroundGradient1;
    internal uint?              BackgroundGradient2;
    internal float?             BackgroundGradientInsetTop;
    internal float?             BackgroundGradientInsetRight;
    internal float?             BackgroundGradientInsetBottom;
    internal float?             BackgroundGradientInsetLeft;
    internal uint?              BackgroundImageIconId;
    internal uint?              BackgroundImageByteSize;
    internal float?             BackgroundImageInsetTop;
    internal float?             BackgroundImageInsetRight;
    internal float?             BackgroundImageInsetBottom;
    internal float?             BackgroundImageInsetLeft;
    internal float?             BackgroundImageRepeatX;
    internal float?             BackgroundImageRepeatY;
    internal uint?              BackgroundImageColor;
    internal short?             BackgroundImageRotation;
    internal byte?              BackgroundImageBlendMode;
    internal uint?              OutlineColor;
    internal float              TextShadowSize;
    internal uint?              TextShadowColor;
    internal Vector4?           DropShadow;
    internal BitmapFontIcon?    BitmapFontIcon;
    internal uint?              IconId;
    internal float?             ImageInsetTop;
    internal float?             ImageInsetRight;
    internal float?             ImageInsetBottom;
    internal float?             ImageInsetLeft;
    internal float?             ImageOffsetX;
    internal float?             ImageOffsetY;
    internal float?             ImageRounding;
    internal RoundedCorners?    ImageRoundedCorners;
    internal ImageScaleMode?    ImageScaleMode;
    internal ImageTileMode?     ImageTileMode;
    internal Vector2?           ImageBlur;
    internal float?             ImageScale;
    internal bool?              ImageGrayscale;
    internal float?             ImageContrast;
    internal short?             ImageRotation;
    internal uint?              ImageColor;
    internal byte?              ImageBlendMode;
    internal uint               UldResourceCrc;
    internal int?               UldPartsId;
    internal int?               UldPartId;
    internal UldStyle?          UldStyle;
    internal bool               IsAntialiased;

    internal static PaintStyleSnapshot Create(ref ComputedStyle style)
    {
        return new() {
            Color                         = style.Color.ToUInt(),
            TextAlign                     = style.TextAlign.Point,
            OutlineSize                   = style.OutlineSize,
            TextOffset                    = style.TextOffset,
            BackgroundColor               = style.BackgroundColor?.ToUInt(),
            BorderTopColor                = style.BorderColor?.Top?.ToUInt(),
            BorderRightColor              = style.BorderColor?.Right?.ToUInt(),
            BorderBottomColor             = style.BorderColor?.Bottom?.ToUInt(),
            BorderLeftColor               = style.BorderColor?.Left?.ToUInt(),
            BorderInsetTop                = style.BorderInset.Top,
            BorderInsetRight              = style.BorderInset.Right,
            BorderInsetBottom             = style.BorderInset.Bottom,
            BorderInsetLeft               = style.BorderInset.Left,
            BorderRadius                  = style.BorderRadius,
            BorderTopWidth                = style.BorderWidth.Top,
            BorderRightWidth              = style.BorderWidth.Right,
            BorderBottomWidth             = style.BorderWidth.Bottom,
            BorderLeftWidth               = style.BorderWidth.Left,
            StrokeColor                   = style.StrokeColor?.ToUInt(),
            StrokeWidth                   = style.StrokeWidth,
            StrokeInset                   = style.StrokeInset,
            StrokeRadius                  = style.StrokeRadius,
            BackgroundGradient1           = style.BackgroundGradient?.Color1?.ToUInt(),
            BackgroundGradient2           = style.BackgroundGradient?.Color2?.ToUInt(),
            BackgroundGradientInsetTop    = style.BackgroundGradientInset.Top,
            BackgroundGradientInsetRight  = style.BackgroundGradientInset.Right,
            BackgroundGradientInsetBottom = style.BackgroundGradientInset.Bottom,
            BackgroundGradientInsetLeft   = style.BackgroundGradientInset.Left,
            BackgroundImageIconId         = style.BackgroundImage is uint image ? image : null,
            BackgroundImageByteSize       = style.BackgroundImage is byte[] bytes ? (uint)bytes.Length : null,
            BackgroundImageInsetBottom    = style.BackgroundImageInset.Bottom,
            BackgroundImageInsetLeft      = style.BackgroundImageInset.Left,
            BackgroundImageInsetRight     = style.BackgroundImageInset.Right,
            BackgroundImageInsetTop       = style.BackgroundImageInset.Top,
            BackgroundImageRepeatX        = style.BackgroundImageScale.X,
            BackgroundImageRepeatY        = style.BackgroundImageScale.Y,
            BackgroundImageColor          = style.BackgroundImageColor.ToUInt(),
            BackgroundImageRotation       = style.BackgroundImageRotation,
            BackgroundImageBlendMode      = (byte)style.BackgroundImageBlendMode,
            RoundedCorners                = style.RoundedCorners,
            OutlineColor                  = style.OutlineColor?.ToUInt(),
            TextShadowSize                = style.TextShadowSize,
            TextShadowColor               = style.TextShadowColor?.ToUInt(),
            DropShadow                    = style.DropShadow,
            BitmapFontIcon                = style.BitmapFontIcon,
            IconId                        = style.IconId,
            ImageInsetTop                 = style.ImageInset?.Top,
            ImageInsetRight               = style.ImageInset?.Right,
            ImageInsetBottom              = style.ImageInset?.Bottom,
            ImageInsetLeft                = style.ImageInset?.Left,
            ImageOffsetX                  = style.ImageOffset?.X,
            ImageOffsetY                  = style.ImageOffset?.Y,
            ImageRounding                 = style.ImageRounding,
            ImageRoundedCorners           = style.ImageRoundedCorners,
            ImageScale                    = style.ImageScale,
            ImageScaleMode                = style.ImageScaleMode,
            ImageTileMode                 = style.ImageTileMode,
            ImageBlur                     = style.ImageBlur,
            ImageGrayscale                = style.ImageGrayscale,
            ImageContrast                 = style.ImageContrast,
            ImageRotation                 = style.ImageRotation,
            ImageColor                    = style.ImageColor.ToUInt(),
            ImageBlendMode                = (byte)style.ImageBlendMode,
            IsAntialiased                 = style.IsAntialiased,
            UldResourceCrc                = Crc32.Get(style.UldResource ?? ""),
            UldPartsId                    = style.UldPartsId,
            UldPartId                     = style.UldPartId,
            UldStyle                      = style.UldStyle,
        };
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.AddBytes(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this), 1)));

        return hash.ToHashCode();
    }
}