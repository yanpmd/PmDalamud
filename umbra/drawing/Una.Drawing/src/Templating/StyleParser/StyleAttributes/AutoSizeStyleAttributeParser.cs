using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal class AutoSizeStyleAttributeParser : IStyleAttributeParser
{
    public bool Apply(Style style, PropertyInfo property, Type attributeType, List<Token> tokens)
    {
        if (attributeType != typeof(ValueTuple<AutoSize, AutoSize>)) return false;
        if (tokens.Count is < 1 or > 2) {
            throw new Exception("Expected one or two tokens for an AutoSize attribute.");
        }

        AutoSize h;
        AutoSize v;

        if (tokens.Count == 1) {
            string name = tokens[0].Value.ToLowerInvariant();

            // Apply the same value to both horizontal and vertical.
            try {
                h = v = Enum.Parse<AutoSize>(name, true);
            } catch (Exception) {
                throw new Exception($"Invalid value '{tokens[0].Value}' for AutoSize attribute.");
            }
        } else {
            string name  = tokens[0].Value.ToLowerInvariant();
            string name2 = tokens[1].Value.ToLowerInvariant();

            try {
                h = Enum.Parse<AutoSize>(name, true);
                v = Enum.Parse<AutoSize>(name2, true);
            } catch (Exception) {
                throw new Exception(
                    $"Invalid value '{tokens[0].Value}' or '{tokens[1].Value}' for AutoSize attribute.");
            }
        }
        
        try {
            property.SetValue(style, new ValueTuple<AutoSize, AutoSize>(h, v));
        } catch (Exception ex) {
            throw new Exception($"Failed to set AutoSize property: {ex.Message}");
        }

        return true;
    }
}