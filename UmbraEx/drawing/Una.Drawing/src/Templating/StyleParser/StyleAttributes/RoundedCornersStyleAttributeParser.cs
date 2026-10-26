using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class RoundedCornersStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (!attributeType.IsEnum || attributeType != typeof(RoundedCorners)) return false;
        if (tokens.Count is > 4 or < 1) {
            throw new Exception("Expected 1 ~ 4 tokens for a RoundedCorners attribute.");
        }

        RoundedCorners rc = RoundedCorners.None;

        foreach (var token in tokens) {
            if (token.Type != TokenType.Identifier && token.Type != TokenType.UInt) {
                throw new Exception(
                    $"Expected an identifier or uint token for a RoundedCorners attribute, but got {token.Type}.");
            }

            string name = token.Value.ToLowerInvariant().Replace("-", "");

            rc |= token.Type switch {
                TokenType.Identifier => Enum.Parse<RoundedCorners>(name, true),
                _ => throw new Exception($"Invalid token type '{token.Type}' for a RoundedCorners attribute.")
            };
        }

        property.SetValue(style, rc);

        return true;
    }
}