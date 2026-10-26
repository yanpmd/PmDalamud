using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class TransitionTypeStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(TransitionType)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for a TransitionType attribute.");
        }

        Token token = tokens.First();

        if (token.Type != TokenType.Identifier && token.Type != TokenType.String) {
            throw new Exception(
                $"Expected an identifier or string token for a TransitionType attribute, but got {token.Type}.");
        }

        try {
            string val = token.Value.Replace("-", "").Trim();
            
            property.SetValue(style, Enum.Parse<TransitionType>(val, true));
        } catch (Exception) {
            throw new Exception($"Invalid value '{token.Value}' for TransitionType attribute.");
        }

        return true;
    }
}