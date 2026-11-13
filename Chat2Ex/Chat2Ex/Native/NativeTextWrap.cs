using System.Text;

namespace Chat2Ex.Native;

/// <summary>
///     Splits text into tokens that can be wrapped. Latin text wraps on spaces, CJK text has to wrap
///     per character because it contains no spaces.
/// </summary>
internal static class NativeTextWrap
{
    public static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var builder = new StringBuilder();

        foreach (var ch in text)
        {
            // Skip game-only glyph placeholders (auto translate markers, icons) and control characters
            // that the font cannot render; they used to show up as stray "}" / "\"" characters.
            if (IsIgnored(ch))
                continue;

            if (ch == '\n')
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                yield return "\n";
                continue;
            }

            if (ch is ' ' or '\t' || IsBreakable(ch))
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                yield return ch.ToString();
                continue;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0)
            yield return builder.ToString();
    }

    /// <summary>
    ///     Characters that must not be rendered: private use area glyphs (game icons / auto translate
    ///     markers) and non printable control characters.
    /// </summary>
    private static bool IsIgnored(char ch) =>
        ch is >= '\ue000' and <= '\uf8ff'   // private use area glyphs
        or < ' ' and not '\n'               // control characters
        or '\u200b'                         // zero width space
        or '\u00ad';                        // soft hyphen

    /// <summary>
    ///     Whether the character itself is a valid wrap position (CJK, fullwidth forms, Hangul).
    /// </summary>
    private static bool IsBreakable(char ch) =>
        ch is >= '\u2e80' and <= '\u9fff'   // CJK radicals, symbols, kana, unified ideographs
        or >= '\u3000' and <= '\u303f'      // CJK punctuation
        or >= '\uff00' and <= '\uffef'      // fullwidth forms
        or >= '\uac00' and <= '\ud7af';     // Hangul syllables
}
