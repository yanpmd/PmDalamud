using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class AnchorStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(Anchor)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for an Anchor attribute.");
        }

        Token token = tokens.First();

        if (token.Type != TokenType.Identifier && token.Type != TokenType.String) {
            throw new Exception($"Expected an identifier or string token for an Anchor attribute, but got {token.Type}.");
        }
        
        string str = token.Value.Replace("-", "").ToLowerInvariant();

        Anchor anchor = str switch {
            "none"         => Anchor.None,
            "topleft"      => Anchor.TopLeft,
            "topcenter"    => Anchor.TopCenter,
            "topright"     => Anchor.TopRight,
            "middleleft"   => Anchor.MiddleLeft,
            "middlecenter" => Anchor.MiddleCenter,
            "middleright"  => Anchor.MiddleRight,
            "bottomleft"   => Anchor.BottomLeft,
            "bottomcenter" => Anchor.BottomCenter,
            "bottomright"  => Anchor.BottomRight,
            _               => throw new Exception("Invalid value for an Anchor attribute.")
        };
        
        property.SetValue(style, anchor);
        
        return true;
    }
}