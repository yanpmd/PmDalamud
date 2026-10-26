using System.Linq;

namespace Una.Drawing.Font;

internal static class FontFactory
{
    internal static IFont CreateFromFontFamily(
        string fontFamily,
        float sizeOffset = 0
    )
    {
        SKFontStyleSet styles    = SKFontManager.Default.GetFontStyles(fontFamily);
        SKFontStyle    fontStyle = styles.FirstOrDefault(
                style => style.Weight >= 400
                    && style.Slant == SKFontStyleSlant.Upright
                )
            ?? (styles.FirstOrDefault() ?? new());

        SKTypeface  srcTypeface = SKTypeface.FromFamilyName(fontFamily, fontStyle);

        return new DynamicFont(srcTypeface, FontRegistry.Glyphs, sizeOffset);
    }

    internal static IFont CreateFromFontFile(FileInfo file, float sizeOffset)
    {
        return new DynamicFont(SKTypeface.FromFile(file.FullName), FontRegistry.Glyphs, sizeOffset);
    }

    internal static IFont CreateFromFontStream(Stream stream, float sizeOffset)
    {
        return new DynamicFont(SKTypeface.FromStream(stream), FontRegistry.Glyphs, sizeOffset);
    }
}
