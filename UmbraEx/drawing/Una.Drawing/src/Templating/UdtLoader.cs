using System.Linq;
using System.Reflection;
using System.Text;

namespace Una.Drawing;

public static class UdtLoader
{
    private static readonly HashSet<string>   CircularReferences = [];
    private static readonly HashSet<Assembly> Assemblies         = [];

    internal static readonly List<IUdtAttributeValueParser>                AttributeValueParsers = [];
    internal static readonly Dictionary<string, List<IUdtDirectiveParser>> DirectiveParsers      = [];

    /// <summary>
    /// <para>
    /// Registers the given assembly to load UDT resources from when using the
    /// <see cref="Load"/> method.
    /// </para>
    /// <para>
    /// Note that import directives inside UDT XML files always use this method
    /// to resolve referenced resources. This means that it is generally safer
    /// to always register an assembly when using <see cref="LoadFromAssembly"/>.
    /// </para>
    /// </summary>
    /// <param name="assembly"></param>
    public static void AddAssembly(Assembly assembly)
    {
        Assemblies.Add(assembly);
    }

    /// <summary>
    /// Removes the given assembly from the internal assemblies list to load
    /// UDT resources from. Resources can no longer be resolved from the given
    /// assembly once removed.
    /// </summary>
    /// <param name="assembly">The assembly to remove from the list.</param>
    public static void RemoveAssembly(Assembly assembly)
    {
        Assemblies.Remove(assembly);
    }

    /// <summary>
    /// Loads a UDT XML file from one of the registered assemblies.
    /// </summary>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="throwOnFailure">Throw an exception on failure. Default is true.</param>
    /// <exception cref="FileNotFoundException">If the resource does not exist.</exception>
    /// <exception cref="InvalidOperationException">When a circular reference is detected.</exception>
    public static UdtDocument Load(string resourceName, bool throwOnFailure = true)
    {
        foreach (var assembly in Assemblies) {
            var resource = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(r => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

            if (resource != null) {
                return LoadFromAssembly(assembly, resourceName, throwOnFailure);
            }
        }

        return throwOnFailure
            ? throw new FileNotFoundException($"Resource '{resourceName}' not found in assembly resources.")
            : new(resourceName, null, null, []);
    }

    /// <summary>
    /// Loads a UDT XML file from embedded resources in the given assembly.
    /// </summary>
    /// <param name="assembly">The assembly to load the resource from.</param>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="throwOnFailure">Throw an exception on failure. Default is true.</param>
    /// <exception cref="FileNotFoundException">If the resource does not exist.</exception>
    /// <exception cref="InvalidOperationException">When a circular reference is detected.</exception>
    public static UdtDocument LoadFromAssembly(Assembly assembly, string resourceName, bool throwOnFailure = true)
    {
        var resource = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

        if (resource == null) {
            throw new FileNotFoundException($"Resource '{resourceName}' not found in assembly resources.");
        }

        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream == null) {
            throw new FileNotFoundException($"Failed to load resource stream for '{resourceName}'.");
        }

        if (!CircularReferences.Add(resource)) {
            StringBuilder sb = new();
            sb.AppendLine($"Circular reference detected while loading UDT \"{resourceName}\":");

            sb.AppendLine($"  ---> \"{CircularReferences.ElementAt(0)}\":");

            for (var i = 1; i < CircularReferences.Count; i++) {
                sb.AppendLine($"  |    \"{CircularReferences.ElementAt(i)}\"");
            }

            sb.AppendLine($"  ---> \"{resource}\"");

            throw new InvalidOperationException(sb.ToString());
        }

        try {
            using var   reader   = new StreamReader(stream);
            string      code     = reader.ReadToEnd();
            UdtDocument document = Parse(resourceName, code, assembly, throwOnFailure);

            return document;
        } catch (Exception e) {
            DalamudServices.PluginLog.Error(
                $"An exception occurred while loading UDT \"{resourceName}\":\n{e.Message}");
            DalamudServices.PluginLog.Error(e.StackTrace ?? " - No stack trace available - ");

            if (throwOnFailure) {
                throw;
            }
        } finally {
            CircularReferences.Remove(resource);
        }

        return new(resourceName, null, null, []);
    }

    /// <summary>
    /// Parses the given UDT XML code and returns a <see cref="UdtDocument"/>.
    /// </summary>
    /// <param name="filename">The original name of the file.</param>
    /// <param name="code">The source code of the template file.</param>
    /// <param name="assembly">The assembly from which this template is loaded. Used for importing other resources.</param>
    /// <param name="throwOnFailure">Throw an exception on failure. Default is true.</param>
    public static UdtDocument Parse(string filename, string code, Assembly? assembly = null, bool throwOnFailure = true)
    {
        if (throwOnFailure) {
            using UdtParser parser = new UdtParser(filename, code, assembly);
            return parser.Parse();
        }

        try {
            using UdtParser parser = new UdtParser(filename, code, assembly);
            return parser.Parse();
        } catch (Exception e) {
            DebugLogger.Error(e.Message);
            DebugLogger.Error(e.StackTrace ?? " - No stack trace available - ");

            return new(filename, null, null, []);
        }
    }

    /// <summary>
    /// Registers an <see cref="IUdtAttributeValueParser"/> that takes in an
    /// attribute value and returns a modified version of it if needed.
    /// </summary>
    /// <param name="parser"></param>
    public static void RegisterAttributeValueParser(IUdtAttributeValueParser parser)
    {
        if (AttributeValueParsers.Contains(parser)) return;
        AttributeValueParsers.Add(parser);
    }

    /// <summary>
    /// Registers a <see cref="IUdtDirectiveParser"/> that can act on a custom
    /// attribute and modify a node if needed.
    /// </summary>
    /// <param name="parser"></param>
    public static void RegisterDirectiveParser(IUdtDirectiveParser parser)
    {
        if (!DirectiveParsers.TryGetValue(parser.Name, out var parserList)) {
            DirectiveParsers.Add(parser.Name, parserList = ([]));
        }

        if (parserList.Contains(parser)) return;
        parserList.Add(parser);
    }

    internal static void Dispose()
    {
        foreach (var p in AttributeValueParsers) p.Dispose();
        AttributeValueParsers.Clear();
    }
}