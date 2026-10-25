using Dalamud.Game.Text;
using Dalamud.Interface;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Una.Drawing.NodeParser;
using Una.Drawing.Templating.StyleParser;

namespace Una.Drawing;

internal sealed partial class UdtParser
{
    internal Node ParseNode(XmlElement element)
    {
        string name = NormalizeElementName(element.Name);

        if (Templates.TryGetValue(name, out var template)) {
            return ConstructNodeFromTemplate(element, template);
        }

        Dictionary<string, string> attributes = [];
        foreach (var attr in element.Attributes.OfType<XmlAttribute>()) {
            string attrName  = NormalizeElementName(attr.Name);
            string attrValue = attr.Value.Trim();

            // Special case: A "slot" attribute is not meant for nodes, but used for templates
            //               to indicate in which portion of the template this node should be placed. 
            if (attrName == "slot") continue;

            attributes.Add(attrName, attrValue);
        }

        // TODO: Detect self-referencing nodes.
        
        Node node = ConstructNode(name, attributes, true);

        if (element.IsEmpty) {
            return node;
        }

        foreach (XmlNode child in element.ChildNodes) {
            if (child is not XmlElement childElement) {
                continue;
            }

            Node childNode = ParseNode(childElement);
            node.ChildNodes.Add(childNode);
        }

        return node;
    }

    internal Node ConstructNode(string name, Dictionary<string, string> attributes, bool addStylesheet = false)
    {
        if (name is "template" or "import") {
            throw new Exception($"Failed to parse UDT \"${Filename}\": Element \"{name}\" is prohibited here.");
        }

        Type  nodeType = GetNodeType(name);
        Node? node     = (Node?)Activator.CreateInstance(nodeType);

        if (node == null) {
            throw new Exception(
                $"Failed to create an instance of \"{nodeType.Name}\", referenced by element \"{name}\" in UDT \"{Filename}\".");
        }

        if (addStylesheet && node.Stylesheet == null) {
            node.Stylesheet = Stylesheet;
        }

        foreach (var (attrName, value) in attributes) {
            string attrValue = ParseAttributeValue(value.Trim());

            if (UdtLoader.DirectiveParsers.TryGetValue(attrName, out List<IUdtDirectiveParser>? parsers)) {
                foreach (var p in parsers) p.Parse(node, attrValue);
                continue;
            }

            switch (attrName) {
                case "id":
                    node.Id = attrValue;
                    break;
                case "class":
                    attrValue.Split(" ", StringSplitOptions.RemoveEmptyEntries)
                             .ToList()
                             .ForEach(c => node.ClassList.Add(c));
                    break;
                case "tags":
                    attrValue.Split(" ", StringSplitOptions.RemoveEmptyEntries)
                             .ToList()
                             .ForEach(c => node.TagsList.Add(c));
                    break;
                case "style":
                    node.Style = StyleParser.StyleFromCode(attrValue);
                    break;
                case "value" when node.GetType().GetProperty("Value") is null:
                    node.NodeValue = attrValue;
                    break;
                default:
                    NodeAttributeParser.ApplyProperty(node, name, attrName, attrValue);
                    break;
            }
        }

        return node;
    }

    private Type GetNodeType(string name)
    {
        name = NormalizeElementName(name);
        if (name is "node" or "n") return typeof(Node);

        try {
            return ElementRegistry.GetElementType(name);
        } catch (Exception ex) {
            throw new Exception($"Failed to construct element \"{name}\" in UDT \"{Filename}\". {ex.Message}", ex);
        }
    }
    
    private static string ParseAttributeValue(string str)
    {
        foreach (var parser in UdtLoader.AttributeValueParsers) {
            str = parser.Parse(str);
        }
        
        return str;
    }
}