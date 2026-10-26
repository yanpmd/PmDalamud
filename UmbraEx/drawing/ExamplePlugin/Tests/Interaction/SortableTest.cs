using Dalamud.Bindings.ImGui;
using Una.Drawing;

namespace ExamplePlugin.Tests.Interaction;

internal class SortableTest : SimpleUdtTest
{
    public override    string Name        => "Sortable Nodes";
    public override    string Category    => "Interaction";
    protected override string UdtFileName => "interaction.sortable_test.xml";

    protected override void OnDocumentLoaded()
    {
    }

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped("Tests the functionality of sortable nodes.");
    }
}
