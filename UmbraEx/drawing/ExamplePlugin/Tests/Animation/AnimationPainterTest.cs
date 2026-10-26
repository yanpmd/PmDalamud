namespace ExamplePlugin.Tests.Animation;

internal class AnimationPainterTest : SimpleUdtTest
{
    public override    string Name        => "Painter Test";
    public override    string Category    => "Animation";
    protected override string UdtFileName => "tests.animation.animation_painter_test.xml";

    protected override void OnDocumentLoaded()
    {
        foreach (var node in Document!.RootNode!.QuerySelectorAll(".cell")) {
            node.OnClick += _ => { };
        }
    }
}