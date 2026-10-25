using Dalamud.Bindings.ImGui;

namespace ExamplePlugin.Tests.Animation;

internal class AnimationPerformanceTest : SimpleUdtTest
{
    public override    string Name        => "Performance Test";
    public override    string Category    => "Animation";
    protected override string UdtFileName => "tests.animation.animation_performance_test.xml";

    protected override void OnDocumentLoaded()
    {
        foreach (var node in Document!.RootNode!.QuerySelectorAll(".box")) {
            node.OnClick += _ => { };
        }
    }

    protected override void OnRenderConfig()
    {
        bool withLayout = Document!.RootNode!.ClassList.Contains("with-layout");
        
        if (ImGui.Checkbox("Affect box size", ref withLayout)) {
            Document.RootNode.ToggleClass("with-layout", withLayout);
        }
    }
}