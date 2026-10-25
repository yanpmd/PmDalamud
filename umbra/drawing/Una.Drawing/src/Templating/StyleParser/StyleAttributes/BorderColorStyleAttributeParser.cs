using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class BorderColorStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(BorderColor)) return false;
        if (tokens.Count is not (1 or 2 or 4)) {
            throw new Exception("Expected 1, 2 or 4 tokens for a Color attribute.");
        }

        List<Color> colors = [];

        foreach (var token in tokens) {
            switch (token.Type) {
                case TokenType.String or TokenType.Identifier:
                    colors.Add(new(token.Value));
                    break;
                case TokenType.UInt:
                    try {
                        colors.Add(new(Convert.ToUInt32(token.Value, 16)));
                    } catch (Exception e) {
                        throw new Exception($"Invalid border-color value '{token.Value}'. {e.Message}");
                    }

                    break;
                case TokenType.Integer when token.Value.Equals("0"):
                    colors.Add(new Color(0));
                    break;
                default:
                    throw new Exception($"Invalid token type '{token.Type}' for a Color attribute.");
            }
        }

        property.SetValue(style, colors.Count switch {
            1 => new BorderColor(colors[0]),
            2 => new BorderColor(colors[0], colors[1]),
            4 => new BorderColor(colors[0], colors[1], colors[2], colors[3]),
            _ => throw new Exception("Invalid number of values for Color attribute. Expected 1, 2 or 4 values.")
        });

        return true;
    }
}