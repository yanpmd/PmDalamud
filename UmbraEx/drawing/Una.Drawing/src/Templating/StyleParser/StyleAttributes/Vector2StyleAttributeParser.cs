using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class Vector2StyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(Vector2)) return false;

        switch (tokens.Count) {
            case 1:
                float f = ParseFloat(tokens[0]);
                property.SetValue(style, new Vector2(f, f));
                return true;
            case 2:
                property.SetValue(style, new Vector2(ParseFloat(tokens[0]), ParseFloat(tokens[1])));
                return true;
        }
        
        throw new Exception("Expected 1 or 2 tokens for a Vector2 attribute.");
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