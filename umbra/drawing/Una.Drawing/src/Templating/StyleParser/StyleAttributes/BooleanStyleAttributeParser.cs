using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class BooleanStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(bool)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for a boolean attribute.");
        }

        try {
            property.SetValue(style, Convert.ToBoolean(tokens.First().Value));
        } catch (Exception ex) {
            throw new Exception($"Failed to set property '{property.Name}': {ex.Message}");
        }
        
        return true;
    }
}