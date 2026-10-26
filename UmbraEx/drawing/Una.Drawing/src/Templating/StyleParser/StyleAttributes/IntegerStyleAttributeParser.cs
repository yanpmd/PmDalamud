using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class IntegerStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(int) && attributeType != typeof(short)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for an integer attribute.");
        }

        switch (attributeType) {
            case not null when attributeType == typeof(int):
                try {
                    property.SetValue(style, Convert.ToInt32(tokens.First().Value));
                } catch (Exception ex) {
                    throw new Exception($"Failed to set property '{property.Name}': {ex.Message}");
                }

                break;
            case not null when attributeType == typeof(short):
                try {
                    property.SetValue(style, Convert.ToInt16(tokens.First().Value));
                } catch (Exception ex) {
                    throw new Exception($"Failed to set property '{property.Name}': {ex.Message}");
                }

                break;
        }

        return true;
    }
}