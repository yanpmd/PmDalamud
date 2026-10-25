using System.Xml;

namespace Una.Drawing;

public class UdtDocument(
    string                         fileName,
    Node?                          rootNode,
    Stylesheet?                    stylesheet,
    Dictionary<string, XmlElement> templates
)
{
    /// <summary>
    /// The name of the file this document was constructed from.
    /// </summary>
    public string FileName { get; } = fileName;

    /// <summary>
    /// The root <see cref="Node"/> that was constructed from the UDT file or
    /// NULL if the file did not produce a root node.
    /// </summary>
    public Node? RootNode { get; } = rootNode;

    /// <summary>
    /// The <see cref="Stylesheet"/> that was constructed from the UDT file or
    /// NULL if the file did not produce a stylesheet.
    /// </summary>
    public Stylesheet? Stylesheet { get; } = stylesheet;

    internal Dictionary<string, XmlElement> Templates { get; } = templates;

    public Node CreateNodeFromTemplate(string name, Dictionary<string, string>? attributes = null)
    {
        // Normalize name.
        name = name.ToLowerInvariant().Replace("-", "").Trim();

        if (!Templates.ContainsKey(name)) {
            throw new ArgumentException(
                $"Template '{name}' not found in UDT document. Available templates: {string.Join(", ", Templates.Keys)}");
        }

        Node node = UdtParser.CreateTemplateFromUdt(this, name, attributes ?? []);
        node.Stylesheet = Stylesheet;

        return node;
    }
}