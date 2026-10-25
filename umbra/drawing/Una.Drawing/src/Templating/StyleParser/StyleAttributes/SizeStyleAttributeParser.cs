using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class SizeStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(Size)) return false;
        if (tokens.Count is not (1 or 2)) {
            throw new Exception("Expected 1 or 2 tokens for a Size attribute.");
        }
        
        var values = new int[tokens.Count];
        
        for (var i = 0; i < tokens.Count; i++) {
            try {
                values[i] = Convert.ToInt32(tokens[i].Value);
            } catch (Exception) {
                throw new Exception($"Invalid value '{tokens[i].Value}' at index #{i + 1} for EdgeSize attribute.");
            }
        }

        Size edgeSize = values.Length switch {
            1 => new Size(values[0]),
            2 => new Size(values[0], values[1]),
            _ => throw new Exception("Invalid number of values for EdgeSize attribute. Expected 1, 2 or 4 values.")
        };
        
        property.SetValue(style, edgeSize);
        
        return true;
    }
}
