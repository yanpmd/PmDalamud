using Dalamud.Bindings.ImGui;

namespace ExamplePlugin.Tests.BoxModel;

internal sealed class PaddingTest : SimpleUdtTest
{
    public override string Name     => "Padding";
    public override string Category => "Box Model";

    protected override string UdtFileName => "box_model.padding_test.xml";

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped(
            "Tests all permutations of padding configurations.\n\nPadding in UDT follows the CSS border-box sizing model.\n\nPadding is the space between the content and the border of the element.\n\nPadding can be set in 4 different ways:\n- Padding: 10; (all edges)\n- Padding: 10 20; (10 vertical, 20 horizontal)\n- Padding: 10 20 30; (top 10, 20 horizontal, 30 bottom)\n- Padding: 10 20 30 40; (top 10, right 20, bottom 30, left 40)"
        );
    }
}