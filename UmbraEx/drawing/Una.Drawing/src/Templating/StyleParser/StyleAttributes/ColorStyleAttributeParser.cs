using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class ColorStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(Color)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for a Color attribute.");
        }

        Token token = tokens.First();

        try {
            property.SetValue(style, token.Type switch {
                TokenType.Identifier or TokenType.String => new Color(token.Value),
                TokenType.UInt => new Color(Convert.ToUInt32(token.Value, 16)),
                TokenType.Integer when token.Value.Equals("0") => new Color(0),
                _ => throw new Exception($"Invalid token type '{token.Type}' for a Color attribute.")
            });
        } catch (Exception ex) {
            throw new Exception($"Invalid color value '{token.Value}': {ex.Message}");
        }

        return true;
    }
}