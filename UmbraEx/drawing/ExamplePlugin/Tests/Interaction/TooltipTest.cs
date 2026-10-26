using Dalamud.Bindings.ImGui;

namespace ExamplePlugin.Tests.Interaction;

internal class TooltipTest : SimpleUdtTest
{
    public override    string Name        => "Tooltips";
    public override    string Category    => "Interaction";
    protected override string UdtFileName => "interaction.tooltip_test.xml";

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped("Tests the functionality of tooltip texts on nodes.\n\nWhen a tooltip is set, the node is automatically marked as 'interactable'. Use the Node Tree inspector to see the interaction variables change as you hover the node.");
    }
}
