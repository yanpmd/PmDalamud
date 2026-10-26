namespace Una.Drawing;

public static class ElementRegistry
{
    private static readonly Dictionary<string, Type> ElementTypes = [];
    
    /// <summary>
    /// <para>
    /// Registers an element in the registry that can be used in template
    /// scripts. If another element with the same name is already registered,
    /// it will be replaced.
    /// </para>
    /// <para>
    /// The element name is the lowercase version of the type name. For example,
    /// the type "<c>MyCustomElement</c>" will be registered as "mycustomelement". The
    /// template parser allows kebab-case names, so "<c>&lt;my-custom-element/&gt;</c>" will
    /// also be matched to "MyCustomElement".
    /// </para>
    /// </summary>
    /// <typeparam name="T">A type that extends from <see cref="Node"/>.</typeparam>
    public static void Register<T>() where T : Node
    {
        // Add a shortcut for the default Node type.
        if (typeof(T).Name == "Node") ElementTypes["n"] = typeof(Node);
        
        ElementTypes[typeof(T).Name.ToLowerInvariant()] = typeof(T);
    }
    
    internal static Type GetElementType(string name)
    {
        name = name.ToLowerInvariant().Replace("-", "").Trim();
        if (ElementTypes.TryGetValue(name, out var type)) return type;
        
        throw new Exception($"Element type '{name}' not found in registry.");
    }

    internal static void Dispose()
    {
        ElementTypes.Clear();
    }
}
