using Dalamud.Bindings.ImGui;
using System;
using Una.Drawing;

namespace ExamplePlugin.Tests.BoxModel;

internal sealed class NestedAutoSizeTest : SimpleUdtTest
{
    public override string Name     => "Complex Auto Size";
    public override string Category => "Box Model";

    protected override string UdtFileName => "box_model.nested_auto_size_test.xml";

    private bool _animate;
    
    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped("Tests the functionality of the auto-size style attribute in a complex layout.");
        ImGui.Separator();
        ImGui.Checkbox("Animate", ref _animate);
    }
    
    protected override void OnRenderTest(ImDrawListPtr drawList)
    {
        if (!_animate) return;

        float width = 800f + MathF.Sin(CurrentTime * 0.01f) * 200f;
        float height = 600f + MathF.Cos(CurrentTime * 0.01f) * 100f;

        Document!.RootNode!.Style.Size = new(width, height);
    }
}