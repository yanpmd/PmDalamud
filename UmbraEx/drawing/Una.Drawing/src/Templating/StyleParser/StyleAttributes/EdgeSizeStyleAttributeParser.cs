using System.Globalization;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class EdgeSizeStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(EdgeSize)) return false;
        if (tokens.Count is not (1 or 2 or 4)) {
            throw new Exception("Expected 1, 2 or 4 tokens for an EdgeSize attribute.");
        }
        
        // Convert rawValues to integers.
        var values = new float[tokens.Count];
        
        for (var i = 0; i < tokens.Count; i++) {
            try {
                values[i] = Convert.ToSingle(tokens[i].Value, CultureInfo.InvariantCulture);
            } catch (Exception) {
                throw new Exception($"Invalid value '{tokens[i].Value}' at index #{i + 1} for EdgeSize attribute.");
            }
        }

        EdgeSize edgeSize = values.Length switch {
            1 => new EdgeSize(values[0]),
            2 => new EdgeSize(values[0], values[1]),
            4 => new EdgeSize(values[0], values[1], values[2], values[3]),
            _ => throw new Exception("Invalid number of values for EdgeSize attribute. Expected 1, 2 or 4 values.")
        };
        
        property.SetValue(style, edgeSize);
        
        return true;
    }
}
