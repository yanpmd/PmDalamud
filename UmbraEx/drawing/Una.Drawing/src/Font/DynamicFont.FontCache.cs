namespace Una.Drawing.Font;

internal partial class DynamicFont
{
    private Dictionary<int, SKFont> TextFontCache  { get; } = [];
    private Dictionary<int, SKFont> GlyphFontCache { get; } = [];

    private SKFont GetTextFont(int fontSize)
    {
        if (TextFontCache.TryGetValue(fontSize, out SKFont? cachedFont)) return cachedFont;

        var font = new SKFont(TextTypeface, fontSize + SizeOffset);
        font.Edging           = SKFontEdging.SubpixelAntialias;
        font.ForceAutoHinting = true;
        font.Subpixel         = true;
        font.Embolden         = false;
        font.BaselineSnap     = false;
        font.EmbeddedBitmaps  = true;
        font.LinearMetrics    = false;

        TextFontCache[fontSize] = font;

        return font;
    }

    private SKFont GetGlyphFont(int fontSize)
    {
        if (GlyphFontCache.TryGetValue(fontSize, out SKFont? cachedFont)) return cachedFont;

        var font = new SKFont(GlyphTypeface, fontSize + SizeOffset);
        font.Edging           = SKFontEdging.SubpixelAntialias;
        font.ForceAutoHinting = true;
        font.Subpixel         = true;
        font.Embolden         = false;
        font.BaselineSnap     = false;
        font.EmbeddedBitmaps  = true;
        font.LinearMetrics    = true;

        GlyphFontCache[fontSize] = font;

        return font;
    }
}