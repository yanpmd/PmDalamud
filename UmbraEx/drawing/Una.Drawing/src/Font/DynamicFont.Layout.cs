namespace Una.Drawing.Font;

internal partial class DynamicFont
{
    private SKFont GetFont(Chunk chunk, int fontSize)
    {
        return chunk.Type == Chunk.Kind.Glyph ? GetGlyphFont(fontSize) : GetTextFont(fontSize);
    }

    /// <summary>
    /// Computes the width of the given chunk based on the specified font size
    /// and returns a new chunk with a cut-off text that fits within the
    /// specified maximum width.
    /// </summary>
    /// <returns>True if the original chunk fits, false otherwise.</returns>
    private bool BreakChunkAt(Chunk chunk, float x, float maxWidth, int fontSize, out Chunk result)
    {
        var font  = GetFont(chunk, fontSize);
        int bytes = font.BreakText(chunk.Text, maxWidth - x);

        if (bytes < chunk.Text.Length) {
            string str = bytes > 2 ? (chunk.Text[..(bytes - 2)] + '…') : chunk.Text[..bytes];
            
            result = new(
                chunk.Type, 
                str,
                chunk.Color,
                chunk.EdgeColor,
                font.MeasureText(str)
            );
            
            return false;
        }

        result = chunk;
        return true;
    }
}
