using Dalamud.Bindings.ImGui;
using System;
using Una.Drawing;

namespace ExamplePlugin.Tests.Interaction;

internal class LotsANodesLayoutTest : SimpleUdtTest
{
    public override    string Name        => "1000 reflow-inducing nodes";
    public override    string Category    => "Performance";
    protected override string UdtFileName => "performance.lots_a_nodes_test.xml";

    protected override void OnDocumentLoaded()
    {
        Node root = Document!.RootNode!;

        int index = 0;
        
        for (var y = 0; y < 20; y++) {
            Node col = new() { ClassList = ["row"] };
            for (var x = 0; x < 50; x++) {
                Node cell = new() { Id = $"C{index}", ClassList = ["cell"] };
                // cell.OnClick += _ => { };
                index++;
                col.AppendChild(cell);
            }
            
            root.AppendChild(col);
        }
    }

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped(
            "Renders 1000 nodes that cause a reflow on every frame."
        );
    }

    private int    _index;
    private Random _random = new();

    protected override void OnRenderTest(ImDrawListPtr dl)
    {
        Document?.RootNode?.QuerySelector($"#C{_index}")?.ToggleClass("highlight2", false);
        
        _index = _random.Next(0, 1000);
        
        Document?.RootNode?.QuerySelector($"#C{_index}")?.ToggleClass("highlight2", true);
    }
}