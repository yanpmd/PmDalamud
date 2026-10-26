namespace Una.Drawing;

public class Animation(ComputedStyle source, ComputedStyle target)
{
    public bool IsPlaying { get; private set; } = true;

    public ComputedStyle SourceStyle { get; } = source;
    public ComputedStyle TargetStyle { get; } = target;

    private Easing.Function _easing = Easing.GetEasingFunction(target.TransitionType);
    private double          _time;
    private double          _progress;

    internal ComputedStyle Update(double deltaTime)
    {
        _time     += deltaTime;
        _progress =  Math.Clamp(_time / TargetStyle.TransitionDuration, 0f, 1f);

        if (_progress >= 1f) {
            IsPlaying = false;
            return TargetStyle;
        }

        var style = SourceStyle;

        UpdateStyle(ref style);

        return style;
    }

    private void UpdateStyle(ref ComputedStyle cs)
    {
        cs.IsVisible                 = TargetStyle.IsVisible;
        cs.Size                      = Interpolate(SourceStyle.Size, TargetStyle.Size);
        cs.Padding                   = Interpolate(SourceStyle.Padding, TargetStyle.Padding) ?? cs.Padding;
        cs.Margin                    = Interpolate(SourceStyle.Margin, TargetStyle.Margin) ?? cs.Margin;
        cs.Color                     = Interpolate(SourceStyle.Color, TargetStyle.Color) ?? cs.Color;
        cs.FontSize                  = Interpolate(SourceStyle.FontSize, TargetStyle.FontSize);
        cs.BorderColor               = Interpolate(SourceStyle.BorderColor, TargetStyle.BorderColor);
        cs.BorderRadius              = Interpolate(SourceStyle.BorderRadius, TargetStyle.BorderRadius) ?? cs.BorderRadius;
        cs.BorderWidth               = Interpolate(SourceStyle.BorderWidth, TargetStyle.BorderWidth) ?? cs.BorderWidth;
        cs.BorderInset               = Interpolate(SourceStyle.BorderInset, TargetStyle.BorderInset) ?? cs.BorderInset;
        cs.BackgroundColor           = Interpolate(SourceStyle.BackgroundColor, TargetStyle.BackgroundColor);
        cs.BackgroundGradient        = Interpolate(SourceStyle.BackgroundGradient, TargetStyle.BackgroundGradient);
        cs.BackgroundGradientInset   = Interpolate(SourceStyle.BackgroundGradientInset, TargetStyle.BackgroundGradientInset) ?? cs.BackgroundGradientInset;
        cs.BackgroundImageColor      = Interpolate(SourceStyle.BackgroundImageColor, TargetStyle.BackgroundImageColor) ?? cs.BackgroundImageColor;
        cs.BackgroundImageRotation   = Interpolate(SourceStyle.BackgroundImageRotation, TargetStyle.BackgroundImageRotation);
        cs.BackgroundImageScale      = Interpolate(SourceStyle.BackgroundImageScale, TargetStyle.BackgroundImageScale);
        cs.BackgroundImageInset      = Interpolate(SourceStyle.BackgroundImageInset, TargetStyle.BackgroundImageInset) ?? cs.BackgroundImageInset;
        cs.Opacity                   = Interpolate(SourceStyle.Opacity, TargetStyle.Opacity) ?? cs.Opacity;
        cs.Gap                       = Interpolate(SourceStyle.Gap, TargetStyle.Gap) ?? cs.Gap;
        cs.DropShadow                = Interpolate(SourceStyle.DropShadow, TargetStyle.DropShadow);
        cs.ImageBlur                 = Interpolate(SourceStyle.ImageBlur, TargetStyle.ImageBlur);
        cs.ImageColor                = Interpolate(SourceStyle.ImageColor, TargetStyle.ImageColor) ?? cs.ImageColor;
        cs.ImageScale                = Interpolate(SourceStyle.ImageScale, TargetStyle.ImageScale) ?? cs.ImageScale;
        cs.ImageRotation             = Interpolate(SourceStyle.ImageRotation, TargetStyle.ImageRotation);
        cs.ImageInset                = Interpolate(SourceStyle.ImageInset, TargetStyle.ImageInset) ?? cs.ImageInset;
        cs.ImageContrast             = Interpolate(SourceStyle.ImageContrast, TargetStyle.ImageContrast) ?? cs.ImageContrast;
        cs.LineHeight                = Interpolate(SourceStyle.LineHeight, TargetStyle.LineHeight) ?? cs.LineHeight;
        cs.MaxWidth                  = Interpolate(SourceStyle.MaxWidth, TargetStyle.MaxWidth) ?? cs.MaxWidth;
        cs.OutlineColor              = Interpolate(SourceStyle.OutlineColor, TargetStyle.OutlineColor) ?? cs.OutlineColor;
        cs.OutlineSize               = Interpolate(SourceStyle.OutlineSize, TargetStyle.OutlineSize) ?? cs.OutlineSize;
        cs.StrokeColor               = Interpolate(SourceStyle.StrokeColor, TargetStyle.StrokeColor);
        cs.StrokeWidth               = Interpolate(SourceStyle.StrokeWidth, TargetStyle.StrokeWidth) ?? cs.StrokeWidth;
        cs.StrokeInset               = Interpolate(SourceStyle.StrokeInset, TargetStyle.StrokeInset) ?? cs.StrokeInset;
        cs.TextOffset                = Interpolate(SourceStyle.TextOffset, TargetStyle.TextOffset);
        cs.ScrollbarThumbColor       = Interpolate(SourceStyle.ScrollbarThumbColor, TargetStyle.ScrollbarThumbColor) ?? cs.ScrollbarThumbColor;
        cs.ScrollbarTrackColor       = Interpolate(SourceStyle.ScrollbarTrackColor, TargetStyle.ScrollbarTrackColor) ?? cs.ScrollbarTrackColor;
        cs.ScrollbarThumbActiveColor = Interpolate(SourceStyle.ScrollbarThumbActiveColor, TargetStyle.ScrollbarThumbActiveColor) ?? cs.ScrollbarThumbActiveColor;
        cs.ScrollbarThumbHoverColor  = Interpolate(SourceStyle.ScrollbarThumbHoverColor, TargetStyle.ScrollbarThumbHoverColor) ?? cs.ScrollbarThumbHoverColor;
    }

    #region Interpolation Functions

    private float? Interpolate(float? from, float? to) => from == null && to == null ? null : _easing(from ?? 0f, to ?? 0, _progress);
    private uint   Interpolate(uint   from, uint   to) => (uint)_easing(from, to, _progress);
    private int    Interpolate(int    from, int    to) => (int)_easing(from, to, _progress);
    private byte   Interpolate(byte   from, byte   to) => (byte)_easing(from, to, _progress);
    private short  Interpolate(short  from, short  to) => (short)_easing(from, to, _progress);
    private sbyte  Interpolate(sbyte  from, sbyte  to) => (sbyte)_easing(from, to, _progress);

    private Size Interpolate(Size from, Size to) => new(
        Interpolate(from.Width, to.Width) ?? 0,
        Interpolate(from.Height, to.Height) ?? 0
    );

    private EdgeSize? Interpolate(EdgeSize? from, EdgeSize? to) =>
        from == null && to == null
            ? null
            : new(
                Interpolate(from?.Top ?? 0, to?.Top ?? 0) ?? 0,
                Interpolate(from?.Right ?? 0, to?.Right ?? 0) ?? 0,
                Interpolate(from?.Bottom ?? 0, to?.Bottom ?? 0) ?? 0,
                Interpolate(from?.Left ?? 0, to?.Left ?? 0) ?? 0
            );

    private Color? Interpolate(Color? from, Color? to) =>
        from == null && to == null
            ? null
            : new(
                Interpolate(from?.R ?? 0, to?.R ?? 0),
                Interpolate(from?.G ?? 0, to?.G ?? 0),
                Interpolate(from?.B ?? 0, to?.B ?? 0),
                Interpolate(from?.A ?? 0, to?.A ?? 0)
            );

    private Vector2 Interpolate(Vector2 from, Vector2 to) => new(
        Interpolate(from.X, to.X) ?? 0,
        Interpolate(from.Y, to.Y) ?? 0
    );

    private Vector3 Interpolate(Vector3 from, Vector3 to) => new(
        Interpolate(from.X, to.X) ?? 0,
        Interpolate(from.Y, to.Y) ?? 0,
        Interpolate(from.Z, to.Z) ?? 0
    );

    private Vector4 Interpolate(Vector4 from, Vector4 to) => new(
        Interpolate(from.X, to.X) ?? 0,
        Interpolate(from.Y, to.Y) ?? 0,
        Interpolate(from.Z, to.Z) ?? 0,
        Interpolate(from.W, to.W) ?? 0
    );

    private BorderColor? Interpolate(BorderColor? from, BorderColor? to) =>
        from == null && to == null
            ? null
            : new(
                Interpolate(from?.Top ?? new(0), to?.Top ?? new(0)),
                Interpolate(from?.Right ?? new(0), to?.Right ?? new(0)),
                Interpolate(from?.Bottom ?? new(0), to?.Bottom ?? new(0)),
                Interpolate(from?.Left ?? new(0), to?.Left ?? new(0))
            );

    private GradientColor? Interpolate(GradientColor? from, GradientColor? to) =>
        from == null && to == null
            ? null
            : new(
                Interpolate(from?.Color1 ?? new(0), to?.Color1 ?? new(0)),
                Interpolate(from?.Color2 ?? new(0), to?.Color2 ?? new(0)),
                to?.Type ?? GradientType.Vertical
            );

    #endregion
}