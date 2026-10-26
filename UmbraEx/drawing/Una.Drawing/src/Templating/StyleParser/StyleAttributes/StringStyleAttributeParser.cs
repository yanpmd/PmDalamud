using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class StringStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(string)) return false;
        if (tokens.Count != 1) {
            throw new Exception("Expected a single token for an string attribute.");
        }

        property.SetValue(style, tokens[0].Value);
        return true;
    }
}