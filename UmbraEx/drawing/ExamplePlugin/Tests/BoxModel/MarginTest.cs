using Dalamud.Bindings.ImGui;

namespace ExamplePlugin.Tests.BoxModel;

internal sealed class MarginTest : SimpleUdtTest
{
    public override string Name     => "Margin";
    public override string Category => "Box Model";

    protected override string UdtFileName => "box_model.margin_test.xml";

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped(
            "Tests all permutations of margin configurations.\n\nMargin in UDT follows the CSS border-box sizing model.\n\nMargin is the space around the border of the element.\n\nMargin can be set in 4 different ways:\n- Margin: 10; (all edges)\n- Margin: 10 20; (10 vertical, 20 horizontal)\n- Margin: 10 20 30; (top 10, 20 horizontal, 30 bottom)\n- Margin: 10 20 30 40; (top 10, right 20, bottom 30, left 40)"
        );
    }
}