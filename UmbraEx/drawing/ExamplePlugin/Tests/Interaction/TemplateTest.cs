namespace ExamplePlugin.Tests.Interaction;

internal class TemplateTest : SimpleUdtTest
{
    public override    string Name        => "Template Spawning";
    public override    string Category    => "Interaction";
    protected override string UdtFileName => "tests.interaction.template_test.xml";

    private int _itemCount = 0;

    protected override void OnDocumentLoaded()
    {
        var root = Document!.RootNode!;

        root.QuerySelector("#button")!.OnClick += _ => SpawnNode();
        root.QuerySelector("#remove")!.OnClick += _ => RemoveNode();
    }

    private void SpawnNode()
    {
        _itemCount++;

        var node = Document!.CreateNodeFromTemplate("item", new() { { "count", _itemCount.ToString() } });

        node.Id = $"Item_{_itemCount}";
        Document!.RootNode!.QuerySelector("#list")!.AppendChild(node);

        node.OnClick += n => { n.Dispose(); };
    }

    private void RemoveNode()
    {
        Document!.RootNode!.QuerySelector($"#list > #Item_{_itemCount}")?.Dispose();
        _itemCount--;
    }
}