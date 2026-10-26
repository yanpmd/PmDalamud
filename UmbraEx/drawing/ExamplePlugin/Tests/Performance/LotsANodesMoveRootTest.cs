using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;
using Una.Drawing;

namespace ExamplePlugin.Tests.Interaction;

internal class LotsANodesMoveRootTest : SimpleUdtTest
{
    public override    string Name        => "Move root with 1000 nodes";
    public override    string Category    => "Performance";
    protected override string UdtFileName => "performance.lots_a_nodes_test.xml";

    private bool _interactive = false;
    
    protected override void OnDocumentLoaded()
    {
        Node root = Document!.RootNode!;

        int index = 0;
        
        for (var y = 0; y < 20; y++) {
            Node col = new() { ClassList = ["row"] };
            for (var x = 0; x < 50; x++) {
                Node cell = new() { Id = $"C{index}", ClassList = ["cell"] };
                cell.OnClick    += _ => { };
                cell.IsDisabled =  true;
                index++;
                col.AppendChild(cell);
            }
            
            root.AppendChild(col);
        }
    }

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped(
            "Renders 1000 nodes that continuously move around the screen. The performance should be on par with the non-animated 1000 nodes performance test."
        );
        ImGui.Separator();

        bool isInteractive = _interactive;
        if (ImGui.Checkbox("Make all nodes interactable.", ref isInteractive)) {
            _interactive = isInteractive;
            foreach (Node cell in Document!.RootNode!.QuerySelectorAll(".cell")) {
                cell.IsDisabled = !isInteractive;
            }
        }
    }

    protected override void OnRenderTest(ImDrawListPtr dl)
    {
        float t = CurrentTime;
        
        RootOffset = new Vector2(
            50 + (MathF.Sin(t * 0.0025f) * 40),
            50 + (MathF.Cos(t * 0.0025f) * 40)
        );
    }
}
