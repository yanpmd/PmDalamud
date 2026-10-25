using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class ImageScaleModeStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(ImageScaleMode)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for an ImageScaleMode attribute.");
        }

        Token token = tokens.First();

        if (token.Type != TokenType.Identifier && token.Type != TokenType.String) {
            throw new Exception(
                $"Expected an identifier or string token for an ImageScaleMode attribute, but got {token.Type}.");
        }

        string str = token.Value.Replace("-", "").ToLowerInvariant();

        ImageScaleMode uldStyle = str switch {
            "adapt"    => ImageScaleMode.Adapt,
            "adaptive" => ImageScaleMode.Adapt,
            "original" => ImageScaleMode.Original,
            "fit"      => ImageScaleMode.Original,
            _          => throw new Exception("Invalid value for an ImageScaleMode attribute.")
        };

        property.SetValue(style, uldStyle);

        return true;
    }
}