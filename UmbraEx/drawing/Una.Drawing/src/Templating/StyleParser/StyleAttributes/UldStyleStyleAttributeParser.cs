using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class UldStyleStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(UldStyle)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for a UldStyle attribute.");
        }

        Token token = tokens.First();

        if (token.Type != TokenType.Identifier && token.Type != TokenType.String) {
            throw new Exception(
                $"Expected an identifier or string token for an UldStyle attribute, but got {token.Type}.");
        }

        string str = token.Value.Replace("-", "").ToLowerInvariant();

        UldStyle uldStyle = str switch {
            "default"    => UldStyle.Default,
            "dark"       => UldStyle.Default,
            "light"      => UldStyle.Light,
            "classic"    => UldStyle.Classic,
            "blue"       => UldStyle.TransparentBlue,
            "clear-blue" => UldStyle.TransparentBlue,
            _            => throw new Exception("Invalid value for a UldStyle attribute.")
        };

        property.SetValue(style, uldStyle);

        return true;
    }
}