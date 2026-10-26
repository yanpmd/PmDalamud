using System.Reflection;
using System.Xml;

namespace Una.Drawing;

internal sealed partial class UdtParser : IDisposable
{
    internal string?                        Filename   { get; private set; }
    internal Stylesheet?                    Stylesheet { get; private set; }
    internal Dictionary<string, XmlElement> Templates  { get; } = [];

    private string?   _code;
    private Assembly? _assembly;
    private Node?     _rootNode;

    internal UdtParser(string filename, string code, Assembly? assembly = null)
    {
        Filename  = filename;
        _code     = code;
        _assembly = assembly;
    }

    private UdtParser(UdtDocument document)
    {
        Filename   = document.FileName;
        _rootNode  = document.RootNode;
        Stylesheet = document.Stylesheet;
        Templates  = document.Templates;
    }

    internal static Node CreateTemplateFromUdt(
        UdtDocument                document,
        string                     templateName,
        Dictionary<string, string> attributes)
    {
        return new UdtTemplateFactory(new(document), templateName, attributes).Build();
    }

    internal UdtDocument Parse()
    {
        using StringReader stringReader = new StringReader(_code!);
        using XmlReader xmlReader = XmlReader.Create(
            stringReader, new() { IgnoreWhitespace = true, IgnoreComments = true, }
        );

        XmlDocument doc = new();

        try {
            doc.Load(xmlReader);
        } catch (XmlException ex) {
            throw new InvalidOperationException(
                $"Failed to parse UDT file \"{Filename}\" at {ex.LineNumber}:{ex.LinePosition}. {ex.Message}", ex
            );
        } catch (Exception ex) {
            throw new InvalidOperationException($"Failed to parse UDT file \"{Filename}\". {ex.Message}", ex);
        }

        // Check for the root element.
        if (doc.DocumentElement == null) {
            throw new InvalidOperationException($"UDT file \"{Filename}\" has no root element.");
        }

        if (NormalizeElementName(doc.DocumentElement.Name) != "udt") {
            throw new InvalidOperationException(
                $"UDT file \"{Filename}\" has an invalid root element \"{doc.DocumentElement.Name}\". Expected \"udt\"."
            );
        }

        ParseUdtNode(doc.DocumentElement);

        return new(Filename ?? "<N/A>", _rootNode, Stylesheet, Templates);
    }

    public void Dispose()
    {
        Filename   = null;
        _code      = null;
        _rootNode  = null;
        Stylesheet = null;
        _assembly  = null;
    }

    /// <summary>
    /// Normalizes the element name by converting it to lowercase and removing
    /// hyphens to allow for kebab-case names.
    /// </summary>
    private static string NormalizeElementName(in string name)
    {
        return name.ToLowerInvariant().Replace("-", "").Trim();
    }
}