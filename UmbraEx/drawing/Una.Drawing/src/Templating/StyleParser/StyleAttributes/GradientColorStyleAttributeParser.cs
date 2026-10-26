using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class GradientColorStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(GradientColor)) return false;
        if (tokens.Count != 3) {
            throw new Exception("Expected 3 tokens for a GradientColor attribute.");
        }

        if (!Enum.TryParse<GradientType>(tokens[0].Value, true, out var gradientType)) {
            throw new Exception(
                $"Invalid gradient type '{tokens[0].Value}'. Expected 'radial', 'horizontal', or 'vertical'."
            );
        }

        Color color1 = CreateColorFromToken(tokens[1]);
        Color color2 = CreateColorFromToken(tokens[2]);

        property.SetValue(style, new GradientColor(color1, color2, gradientType));

        return true;
    }

    private static Color CreateColorFromToken(Token token)
    {
        try {
            return token.Type switch {
                TokenType.String => new Color(token.Value),
                TokenType.UInt => new Color(Convert.ToUInt32(token.Value, 16)),
                TokenType.Integer when token.Value.Equals("0") => new Color(0),
                _ => throw new Exception($"Invalid token type '{token.Type}' for color."),
            };
        } catch (Exception e) {
            throw new Exception($"Failed to parse color from token '{token.Value}': {e.Message}");
        }
    }
}