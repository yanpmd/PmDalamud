using Una.Drawing.Templating.StyleParser;

namespace Una.Drawing;

public static class StylesheetRegistry
{
    private static readonly Dictionary<string, Stylesheet> Stylesheets = [];

    /// <summary>
    /// <para>
    /// Registers a stylesheet with the given name and source.
    /// </para>
    /// <para>
    /// The stylesheet can later be referenced in other stylesheets via an <c>@import</c>
    /// statement. This is typically used to share common style rules and mixins.
    /// </para>
    /// <example>
    /// When registering a stylesheet with the name <c>base</c>, it can be imported
    /// in other stylesheets like this:
    /// <code>
    /// @import "base";
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="name">The name of the stylesheet.</param>
    /// <param name="source">The source code.</param>
    public static void Register(string name, string source)
    {
        Stylesheets[name.ToLowerInvariant()] = StyleParser.StylesheetFromCode(source);
    }

    /// <summary>
    /// <para>
    /// Registers a <see cref="Stylesheet"/> with the given name.
    /// </para>
    /// <para>
    /// The stylesheet can later be referenced in other stylesheets via an <c>@import</c>
    /// statement. This is typically used to share common style rules and mixins.
    /// </para>
    /// <example>
    /// When registering a stylesheet with the name <c>base</c>, it can be imported
    /// in other stylesheets like this:
    /// <code>
    /// @import "base";
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="name">The name of the stylesheet.</param>
    /// <param name="stylesheet">The stylesheet instance to register.</param>
    public static void Register(string name, Stylesheet stylesheet)
    {
        Stylesheets[name.ToLowerInvariant()] = stylesheet;
    }

    /// <summary>
    /// Removes a stylesheet with the given name from the registry.
    /// </summary>
    /// <param name="name"></param>
    public static void Unregister(string name)
    {
        Stylesheets.Remove(name.ToLowerInvariant());
    }

    internal static void Dispose()
    {
        Stylesheets.Clear();
    }

    /// <summary>
    /// Returns true if a stylesheet with the given name exists.
    /// </summary>
    /// <param name="name">The name of the stylesheet.</param>
    internal static bool Exists(string name)
    {
        return Stylesheets.ContainsKey(name.ToLowerInvariant());
    }

    /// <summary>
    /// Retrieves a stylesheet with the given name.
    /// </summary>
    /// <param name="name">The name of the stylesheet.</param>
    /// <returns>The stylesheet code.</returns>
    /// <exception cref="Exception"></exception>
    internal static Stylesheet Get(string name)
    {
        if (Stylesheets.TryGetValue(name.ToLowerInvariant(), out var sheet))
            return sheet;

        throw new Exception($"Stylesheet '{name}' not found. Make sure to register it first via StylesheetRegistry.Register()");
    }
}