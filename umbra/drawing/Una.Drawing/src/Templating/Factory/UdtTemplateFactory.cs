using System.Linq;
using System.Xml;

namespace Una.Drawing;

internal class UdtTemplateFactory
{
    private readonly Dictionary<string, string>           _attributes;
    private readonly Dictionary<string, TemplateArgument> _arguments;
    private readonly XmlElement                           _element;
    private readonly XmlElement                           _sourceElement;
    private readonly XmlElement                           _rootElement;
    private readonly UdtParser                            _parser;

    internal UdtTemplateFactory(
        UdtParser                  parser,
        XmlElement                 template,
        XmlElement                 sourceElement,
        Dictionary<string, string> attributes
    )
    {
        _parser        = parser;
        _attributes    = attributes;
        _element       = (template.Clone() as XmlElement)!;
        _sourceElement = sourceElement;
        _arguments     = ParseTemplateArguments();
        _rootElement   = ExtractRootElementFromTemplate();
    }

    internal UdtTemplateFactory(UdtParser parser, string templateName, Dictionary<string, string> attributes)
    {
        var doc = new XmlDocument();
        var el  = doc.CreateElement(templateName);

        foreach ((string k, string v) in attributes) el.SetAttribute(k, v);

        _parser        = parser;
        _attributes    = attributes;
        _element       = parser.Templates[templateName];
        _sourceElement = _element;
        _arguments     = ParseTemplateArguments();
        _rootElement   = ExtractRootElementFromTemplate();
    }

    internal Node Build()
    {
        Node root = ConstructNodeFrom(_rootElement);
        root.Stylesheet = _parser.Stylesheet;

        return root;
    }

    private Node ConstructNodeFrom(XmlElement element)
    {
        // TODO: Detect self-referencing nodes.
        string name = NormalizeElementName(element.Name);

        if (_parser.Templates.TryGetValue(name, out var template)) {
            // TODO: We might lose slot information here from the source node. Needs verification.
            return _parser.ConstructNodeFromTemplate(element, template);
        }

        Dictionary<string, string> attributes = [];

        foreach (var attr in element.Attributes.OfType<XmlAttribute>()) {
            string attrName  = NormalizeElementName(attr.Name);
            string attrValue = ParseTemplateAttribute(attr.Value);

            // IDs are unique and should not be used in templates unless its value
            // is explicitly replaced with a template argument.
            if (attrName == "id" && !attr.Value.Contains("${")) {
                throw new Exception(
                    $"Template \"{TemplateName}\" in \"{_parser.Filename}\" has an \"id\" attribute " +
                    $"with a value that does not contain a template argument.");
            }

            attributes.Add(attrName, attrValue);
        }

        Node node = _parser.ConstructNode(name, attributes);

        foreach (XmlNode child in element.ChildNodes) {
            if (child is not XmlElement childElement) {
                DebugLogger.Log($"WARNING: Child node \"{child.Name}\" is not an element. Skipping.");
                continue;
            }

            string childName = NormalizeElementName(childElement.Name);
            if (childName == "slot" && childElement.IsEmpty) {
                string     slotName        = childElement.GetAttribute("name");
                List<Node> slottedElements = ExtractSlottedElementsFromSource(slotName);

                foreach (Node slottedElement in slottedElements) {
                    node.ChildNodes.Add(slottedElement);
                }

                continue;
            }

            Node childNode = ConstructNodeFrom(childElement);
            node.ChildNodes.Add(childNode);
        }

        return node;
    }

    private List<Node> ExtractSlottedElementsFromSource(string slotName)
    {
        List<Node> slottedElements = [];

        foreach (XmlNode child in _sourceElement.ChildNodes) {
            if (child is not XmlElement childElement) {
                DebugLogger.Log($"WARNING: Child node \"{child.Name}\" is not an element. Skipping.");
                continue;
            }

            string slot = childElement.GetAttribute("slot");

            if ((slotName == "" && slot == "") || (slotName != "" && slot == slotName)) {
                slottedElements.Add(_parser.ParseNode(childElement));
            }
        }

        return slottedElements;
    }

    private string ParseTemplateAttribute(string attributeValue)
    {
        string result = attributeValue;

        foreach (var variableName in ExtractVariablePlaceholdersFromAttributeValue(attributeValue)) {
            string name = NormalizeElementName(variableName);

            if (!_arguments.TryGetValue(name, out var attribute)) {
                throw new Exception(
                    $"The variable \"{variableName}\" was referenced in a template but was not defined as a template argument."
                );
            }

            string? argValue = _attributes.FirstOrDefault(x => x.Key == name).Value ?? attribute.DefaultValue;
            if (argValue == null) {
                throw new Exception(
                    $"Failed to construct element \"{TemplateName}\" in \"{_parser.Filename}\": " +
                    $"Argument \"{name}\" is not defined and has no default value."
                );
            }

            result = result.Replace($"${{{variableName}}}", argValue);
        }

        return result;
    }

    /// <summary>
    /// Extracts variable placeholders from the attribute value.
    /// Variables are formatted like <c>${variableName}</c>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private List<string> ExtractVariablePlaceholdersFromAttributeValue(string value)
    {
        List<string> result = [];

        int startIndex = 0;
        while ((startIndex = value.IndexOf("${", startIndex, StringComparison.InvariantCulture)) != -1) {
            int endIndex = value.IndexOf("}", startIndex, StringComparison.InvariantCulture);
            if (endIndex == -1) break;

            string variableName = value.Substring(startIndex + 2, endIndex - startIndex - 2);
            result.Add(variableName);

            startIndex = endIndex + 1;
        }

        return result;
    }

    /// <summary>
    /// Extracts the root element node from the template.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private XmlElement ExtractRootElementFromTemplate()
    {
        List<XmlElement> rootNodeList =
            _element.ChildNodes
                    .OfType<XmlElement>()
                    .Where(el => NormalizeElementName(el.Name) is not "argument")
                    .ToList();

        return rootNodeList.Count switch {
            0 => throw new Exception(
                $"Template \"{TemplateName}\" in \"{_parser.Filename}\" has no root element."),
            > 1 => throw new Exception(
                $"Template \"{TemplateName}\" in \"{_parser.Filename}\" has multiple root elements."),
            _ => rootNodeList.First()
        };
    }

    /// <summary>
    /// Collects a dictionary of template arguments.
    /// </summary>
    private Dictionary<string, TemplateArgument> ParseTemplateArguments()
    {
        Dictionary<string, TemplateArgument> templateArguments = [];
        foreach (var el in _element.ChildNodes.OfType<XmlElement>()
                                   .Where(t => NormalizeElementName(t.Name) == "argument")
        ) {
            string name = el.GetAttribute("name");
            if (name == "")
                throw new Exception(
                    $"Template \"{TemplateName}\" has an argument in \"{_parser.Filename}\" with no \"name\" attribute.");

            string? value = el.HasAttribute("default")
                ? el.GetAttribute("default")
                : null;
            
            templateArguments.Add(NormalizeElementName(name), new TemplateArgument(value));
        }

        return templateArguments;
    }

    // TODO: Add optional description or other metadata?
    private record TemplateArgument(string? DefaultValue);

    private string TemplateName => _element.GetAttribute("name");

    private static string NormalizeElementName(string name) => name.ToLowerInvariant().Replace("-", "").Trim();
}