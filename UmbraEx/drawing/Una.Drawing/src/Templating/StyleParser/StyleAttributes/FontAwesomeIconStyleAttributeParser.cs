using Dalamud.Interface;
using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class FontAwesomeIconStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(FontAwesomeIcon)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for a FontAwesomeIcon attribute.");
        }

        Token token = tokens.First();

        if (token.Type != TokenType.Identifier && token.Type != TokenType.String) {
            throw new Exception($"Expected an identifier or string token for a FontAwesomeIcon attribute, but got {token.Type}.");
        }
        
        string str = token.Value.Replace("-", "").ToLowerInvariant();

        if (! Enum.TryParse<FontAwesomeIcon>(str, true, out var icon)) {
            throw new Exception($"Invalid FontAwesomeIcon value: {token.Value}.");
        }
        
        property.SetValue(style, icon);
        
        return true;
    }
}