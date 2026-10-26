using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class DoubleStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(float) && attributeType != typeof(double)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for an float or double attribute.");
        }

        switch (attributeType) {
            case not null when attributeType == typeof(float):
                
                if (!float.TryParse(tokens.First().Value, CultureInfo.InvariantCulture, out var floatValue)) {
                    throw new Exception($"Failed to parse float value: {tokens.First().Value}");
                }
                property.SetValue(style, floatValue);
                break;
            case not null when attributeType == typeof(double):
                if (!double.TryParse(tokens.First().Value, CultureInfo.InvariantCulture, out var doubleValue)) {
                    throw new Exception($"Failed to parse double value: {tokens.First().Value}");
                }
                property.SetValue(style, doubleValue);
                break;
            default:
                throw new Exception($"Unsupported attribute type: {attributeType}");
        }
        
        return true;
    }
}