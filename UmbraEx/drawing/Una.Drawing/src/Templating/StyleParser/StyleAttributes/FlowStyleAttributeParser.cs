using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class FlowStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (!attributeType.IsEnum || attributeType != typeof(Flow)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for a Flow attribute.");
        }
        
        Token token = tokens.First();
        if (token.Type != TokenType.Identifier) {
            throw new Exception($"Expected an identifier token for a Flow attribute, but got {token.Type}.");
        }
        
        // Convert the value to the enum type.
        string str = token.Value.Replace("-", "").ToLowerInvariant();

        if (!Enum.TryParse(attributeType, str, true, out var result)) {
            throw new Exception("Invalid value for Flow attribute. Expected 'horizontal' or 'vertical'.");
        }

        property.SetValue(style, result);
        return true;
    }
}
