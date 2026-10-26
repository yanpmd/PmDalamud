using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class UnsignedIntegerStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(uint)) return false;
        
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for an unsigned integer attribute.");
        }

        try {
            property.SetValue(style, Convert.ToUInt32(tokens.First().Value));
        } catch (Exception ex) {
            throw new Exception($"Failed to set property '{property.Name}': {ex.Message}");
        }
        
        return true;
    }
}