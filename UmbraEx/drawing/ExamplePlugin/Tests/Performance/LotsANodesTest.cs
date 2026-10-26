using Dalamud.Bindings.ImGui;
using Una.Drawing;

namespace ExamplePlugin.Tests.Interaction;

internal class LotsANodesTest : SimpleUdtTest
{
    public override    string Name        => "1000 nodes";
    public override    string Category    => "Performance";
    protected override string UdtFileName => "performance.lots_a_nodes_test.xml";

    private bool _animate;
    private bool _interactive;
    
    protected override void OnDocumentLoaded()
    {
        Node root = Document!.RootNode!;

        int index = 0;
        
        for (var y = 0; y < 20; y++) {
            Node col = new() { ClassList = ["row"] };
            for (var x = 0; x < 50; x++) {
                Node cell = new() { Id = $"C{index}", ClassList = ["cell"] };
                cell.OnClick    += _ => { };
                cell.IsDisabled =  !_interactive;
                index++;
                col.AppendChild(cell);
            }
            
            root.AppendChild(col);
        }
    }

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped(
            "Renders 1000 nodes."
        );
        ImGui.Separator();

        bool animate = _animate;
        if (ImGui.Checkbox("Animate nodes.", ref animate)) {
            _animate = animate;
        }
        
        bool isInteractive = _interactive;
        if (ImGui.Checkbox("Make all nodes interactable.", ref isInteractive)) {
            _interactive = isInteractive;
            foreach (Node cell in Document!.RootNode!.QuerySelectorAll(".cell")) {
                cell.IsDisabled = !isInteractive;
            }
        }
    }

    private int _index;

    protected override void OnRenderTest(ImDrawListPtr dl)
    {
        if (!_animate) {
            Document?.RootNode?.QuerySelector($"#C{_index}")?.ToggleClass("highlight", false);
            return;
        }
        
        Document?.RootNode?.QuerySelector($"#C{_index}")?.ToggleClass("highlight", false);
        
        _index++;
        if (_index == 1000) _index = 0;
        
        Document?.RootNode?.QuerySelector($"#C{_index}")?.ToggleClass("highlight", true);
    }
}