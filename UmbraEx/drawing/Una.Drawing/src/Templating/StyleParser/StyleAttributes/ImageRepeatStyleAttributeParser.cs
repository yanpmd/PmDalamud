using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class ImageTileModeStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(ImageTileMode)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for an ImageTileMode attribute.");
        }

        Token token = tokens.First();

        if (token.Type != TokenType.Identifier && token.Type != TokenType.String) {
            throw new Exception(
                $"Expected an identifier or string token for an ImageTileMode attribute, but got {token.Type}.");
        }

        string str = token.Value.Replace("-", "").ToLowerInvariant();

        ImageTileMode uldStyle = str switch {
            "clamp"  => ImageTileMode.Clamp,
            "repeat" => ImageTileMode.Repeat,
            "mirror" => ImageTileMode.Mirror,
            "decal"  => ImageTileMode.Decal,
            "none"   => ImageTileMode.Decal,
            _        => throw new Exception("Invalid value for an ImageTileMode attribute.")
        };

        property.SetValue(style, uldStyle);

        return true;
    }
}