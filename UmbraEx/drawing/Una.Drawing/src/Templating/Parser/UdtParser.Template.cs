using System.Xml;

namespace Una.Drawing;

internal sealed partial class UdtParser
{
    private void ParseTemplateNode(XmlElement element)
    {
        if (!element.HasAttribute("name")) {
            throw new Exception($"A template element in \"{Filename}\" has no \"name\" attribute.");
        }

        string name = NormalizeElementName(element.GetAttribute("name"));
        if (name == "") {
            throw new Exception($"A template element in \"{Filename}\" has an empty \"name\" attribute.");
        }

        Templates.Add(name, element);
    }

    /// <summary>
    /// Constructs a new Node from a template.
    /// </summary>
    /// <param name="sourceElement">The element that references the template.</param>
    /// <param name="templateElement">The template element.</param>
    /// <returns></returns>
    internal Node ConstructNodeFromTemplate(XmlElement sourceElement, XmlElement templateElement)
    {
        // Extract attributes from the source element.
        Dictionary<string, string> attributes = [];
        foreach (XmlAttribute attr in sourceElement.Attributes) {
            string attrName  = NormalizeElementName(attr.Name);
            string attrValue = attr.Value.Trim();
            attributes.Add(attrName, attrValue);
        }

        UdtTemplateFactory tpl = new(this, templateElement, sourceElement, attributes);

        return tpl.Build();
    }
}