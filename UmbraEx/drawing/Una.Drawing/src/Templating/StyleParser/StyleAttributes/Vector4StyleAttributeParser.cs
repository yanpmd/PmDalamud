using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class Vector4StyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(Vector4)) return false;

        switch (tokens.Count) {
            case 1: {
                float f = ParseFloat(tokens[0]);
                property.SetValue(style, new Vector4(f, f, f, f));
                return true;
            }
            case 2: {
                float x = ParseFloat(tokens[0]);
                float y = ParseFloat(tokens[1]);
                property.SetValue(style, new Vector4(x, y, x, y));
                return true;
            }
            case 3: {
                float x = ParseFloat(tokens[0]);
                float y = ParseFloat(tokens[1]);
                float z = ParseFloat(tokens[2]);
                property.SetValue(style, new Vector4(x, y, z, z));
                return true;
            }
            case 4: {
                float x = ParseFloat(tokens[0]);
                float y = ParseFloat(tokens[1]);
                float z = ParseFloat(tokens[2]);
                float w = ParseFloat(tokens[3]);
                property.SetValue(style, new Vector4(x, y, z, w));
                return true;
            }
        }
        
        throw new Exception("Expected 1 to 4 tokens for a Vector4 attribute.");
    }
    
    private static float ParseFloat(Token token)
    {
        switch (token.Type)
        {
            case TokenType.Integer or TokenType.Double:
                return float.Parse(token.Value);
            case TokenType.Identifier:
                return token.Value.ToLower() switch {
                    "left"   => 0,
                    "right"  => 1,
                    "top"    => 0,
                    "bottom" => 1,
                    _        => throw new Exception($"Unknown identifier '{token.Value}' for Vector2 attribute. Expected 'left', 'right', 'top', or 'bottom'.")
                };
            default:
                throw new Exception($"Expected a number or identifier for Vector2 attribute, but got {token.Type}.");
        }
    }
}