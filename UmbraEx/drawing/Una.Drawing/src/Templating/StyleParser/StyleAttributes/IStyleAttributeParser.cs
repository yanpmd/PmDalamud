using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal interface IStyleAttributeParser
{
    internal bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens);
}
