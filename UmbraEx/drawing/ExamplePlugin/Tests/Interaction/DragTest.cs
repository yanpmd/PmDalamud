using Dalamud.Bindings.ImGui;
using Una.Drawing;

namespace ExamplePlugin.Tests.Interaction;

internal class DragTest : SimpleUdtTest
{
    public override    string Name        => "Draggable Nodes";
    public override    string Category    => "Interaction";
    protected override string UdtFileName => "interaction.drag_test.xml";

    protected override void OnDocumentLoaded()
    {
        foreach (Node node in Document!.RootNode!.QuerySelectorAll(".draggable")) {
            node.OnDragStart += _ => { };
            node.OnDragMove += _ => { };
            node.OnDragEnd += _ => { };
        }
    }

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped("Tests the functionality of draggable nodes. The node should be draggable but does not reposition itself after dragging.");
    }
}
