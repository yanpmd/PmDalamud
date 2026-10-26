using System.Text;

namespace Una.Drawing;

internal static class QuerySelectorTokenizer
{
    internal static List<QuerySelectorToken> Tokenize(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return [];

        var tokens = new List<QuerySelectorToken>();
        int i      = 0;
        int len    = selector.Length;
        
        bool expectingSelector = true;

        while (i < len) {
            int whitespaceStart = i;
            while (i < len && char.IsWhiteSpace(selector[i])) {
                i++;
            }

            bool whitespaceSkipped = i > whitespaceStart;

            if (i >= len) break;

            char currentChar = selector[i];
            bool isSelectorStartChar = CanStartIdentifier(currentChar) || currentChar == '.' || currentChar == '#' ||
                                       currentChar == ':' ||
                                       currentChar == '[';

            if (whitespaceSkipped && !expectingSelector && isSelectorStartChar) {
                tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.DeepChild, " "));
            }

            currentChar = selector[i];

            switch (currentChar) {
                case '*':
                    tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.All, "*"));
                    i++;
                    expectingSelector = true;
                    continue;
                case '>':
                    tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.Child, ">"));
                    i++;
                    expectingSelector = true;
                    continue;

                case ',':
                    tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.Separator, ","));
                    i++;
                    expectingSelector = true;
                    continue;

                case '#': {
                    i++;
                    if (i >= len)
                        throw new QuerySelectorParseException("Expected identifier after '#' but found end of string.");
                    string idValue = ReadIdentifier(selector, ref i);
                    if (string.IsNullOrEmpty(idValue))
                        throw new QuerySelectorParseException($"Expected identifier after '#' at position {i - 1}.");
                    tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.Identifier, idValue));
                    expectingSelector = false; // Parsed a selector part
                    continue;
                }

                case '.': {
                    i++;
                    if (i >= len) {
                        throw new QuerySelectorParseException("Expected class name after '.' but found end of string.");
                    }

                    string classValue = ReadIdentifier(selector, ref i);

                    if (string.IsNullOrEmpty(classValue)) {
                        throw new QuerySelectorParseException($"Expected class name after '.' at position {i - 1}.");
                    }

                    tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.Class, classValue));
                    expectingSelector = false;
                    continue;
                }
                case ':': {
                    i++;

                    if (i >= len) {
                        throw new QuerySelectorParseException("Expected tag name after ':' but found end of string.");
                    }

                    string tagValue = ReadIdentifier(selector, ref i);

                    if (string.IsNullOrEmpty(tagValue)) {
                        throw new QuerySelectorParseException(
                            $"Expected pseudo-class/element name after ':' at position {i - 1}."
                        );
                    }

                    tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.TagList, tagValue));
                    expectingSelector = false;
                    continue;
                }

                default:
                    if (CanStartIdentifier(currentChar)) {
                        string typeValue = ReadIdentifier(selector, ref i);

                        tokens.Add(new QuerySelectorToken(QuerySelectorTokenType.Identifier, typeValue));
                        expectingSelector = false;
                        continue;
                    }

                    throw new QuerySelectorParseException(
                        $"Unexpected character '{currentChar}' at position {i} in query selector \"{selector}\"."
                    );
            }
        }

        return tokens;
    }
    
    /// <summary>
    /// Checks if a character is valid within a CSS identifier (selector name, class, ID).
    /// </summary>
    private static bool IsValidIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '-' || c == '_';
    }

    /// <summary>
    /// Checks if a character can start a type selector or be part of other identifiers.
    /// </summary>
    private static bool CanStartIdentifier(char c)
    {
        return char.IsLetter(c) || c == '_';
    }

    /// <summary>
    /// Reads a sequence of valid identifier characters from the string starting at the current index.
    /// Updates the index reference to point after the read identifier.
    /// </summary>
    private static string ReadIdentifier(string str, ref int index)
    {
        var sb = new StringBuilder();
        
        while (index < str.Length && IsValidIdentifierChar(str[index])) {
            sb.Append(str[index]);
            index++;
        }

        return sb.ToString();
    }
}