using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class FlowOrderStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (!attributeType.IsEnum || attributeType != typeof(FlowOrder)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for a FlowOrder attribute.");
        }
        
        Token token = tokens.First();
        if (token.Type != TokenType.Identifier) {
            throw new Exception($"Expected an identifier token for a FlowOrder attribute, but got {token.Type}.");
        }
        
        // Convert the value to the enum type.
        string str = token.Value.Replace("-", "").ToLowerInvariant();

        if (!Enum.TryParse<FlowOrder>(str, true, out var result)) {
            throw new Exception("Invalid value for FlowOrder attribute. Expected 'normal' or 'reverse'.");
        }

        property.SetValue(style, result);
        return true;
    }
}
