using System.Xml;

namespace Una.Drawing;

internal sealed partial class UdtParser
{
    private void ParseImportNode(XmlElement node)
    {
        if (!node.HasAttribute("from")) {
            throw new($"Import node \"{node.Name}\" in \"{Filename}\" has no \"from\" attribute.");
        }

        string resourceName = node.GetAttribute("from");
        if (resourceName == "") {
            throw new($"Import node \"{node.Name}\" in \"{Filename}\" has an empty \"from\" attribute.");
        }

        UdtDocument doc = LoadFromAssembly(resourceName);

        if (doc.RootNode != null) {
            _rootNode = doc.RootNode;
        }

        MergeStylesheetFrom(doc);
        MergeTemplatesFrom(doc);
    }

    private UdtDocument LoadFromAssembly(string resourceName)
    {
        try {
            return UdtLoader.Load(resourceName);
        } catch (Exception err) {
            // Crashing here may cause ImGui to go haywire.
            string msg = $"Failed to load UDT \"{resourceName}\", imported from \"{Filename}\".\n{err.Message}";

            DalamudServices.PluginLog.Error(msg);

            return new(resourceName, new() { NodeValue = msg, Style = new() { Color = new(255, 0, 0), }, }, null, []);
        }
    }

    private void MergeStylesheetFrom(UdtDocument doc)
    {
        if (null == doc.Stylesheet) return;

        Stylesheet ??= new([]);
        Stylesheet.ImportFrom(doc.Stylesheet);
    }

    private void MergeTemplatesFrom(UdtDocument doc)
    {
        if (doc.Templates.Count == 0) return;

        foreach (var template in doc.Templates) {
            if (Templates.ContainsKey(template.Key)) {
                throw new Exception(
                    $"Template \"{template.Key}\" already exists or has already been imported in \"{Filename}\".");
            }

            Templates.Add(template.Key, template.Value);
        }
    }
}