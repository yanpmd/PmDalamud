using System.Linq;
using System.Reflection;

namespace Una.Drawing.Templating.StyleParser;

internal static class StyleAttributeParser
{
    private static readonly List<IStyleAttributeParser>      Parsers    = [];
    private static readonly Dictionary<string, PropertyInfo> Properties = [];

    internal static void ApplyRule(Style style, string identifier, List<Token> tokens)
    {
        if (Parsers.Count == 0) InitializeParsers();
        if (Properties.Count == 0) InitializeProperties();

        identifier = identifier.ToLowerInvariant().Replace("-", "").Trim();
        
        if (!Properties.TryGetValue(identifier, out PropertyInfo? property)) {
            throw new Exception($"Unknown style property: {identifier}");
        }

        // Grab the actual type, or underlying type if the property is nullable.
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        foreach (var parser in Parsers) {
            if (parser.Apply(style, property, type, tokens)) return;
        }
        
        throw new Exception($"Failed to apply style '{identifier}' with value '{string.Join(" ", tokens.Select(t => t.Value))}'.");
    }

    internal static void Dispose()
    {
        Parsers.Clear();
        Properties.Clear();
    }
    
    /// <summary>
    /// Creates a lookup table for style properties.
    /// </summary>
    private static void InitializeProperties()
    {
        Type           type       = typeof(Style);
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (PropertyInfo property in properties) {
            string name = property.Name.ToLowerInvariant();
            Properties.Add(name, property);
        }
    }

    /// <summary>
    /// Instantiates the attribute parsers.
    /// </summary>
    private static void InitializeParsers()
    {
        Type type = typeof(IStyleAttributeParser);
        List<Type> parserTypes =
            Assembly
               .GetExecutingAssembly().GetTypes()
               .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(type))
               .ToList();

        foreach (Type parserType in parserTypes) {
            Parsers.Add((IStyleAttributeParser)Activator.CreateInstance(parserType)!);
        }
    }
}