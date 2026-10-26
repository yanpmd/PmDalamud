using Una.Drawing.Texture;

namespace Una.Drawing.Font;

internal readonly struct Chunk(
    Chunk.Kind kind,
    string     text,
    Color?     color,
    Color?     edgeColor,
    float      width,
    GfdIcon?   icon = null
)
{
    internal enum Kind
    {
        Text,
        Glyph,
        BitmapIcon,
    }

    internal Kind     Type       { get; } = kind;
    internal string   Text       { get; } = text;
    internal Color?   Color      { get; } = color;
    internal Color?   EdgeColor  { get; } = edgeColor;
    internal float    Width      { get; } = width;
    internal GfdIcon? BitmapIcon { get; } = icon;
}