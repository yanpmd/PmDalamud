namespace ExamplePlugin.Tests.Animation;

internal class AnimationTest : SimpleUdtTest
{
    public override    string Name        => "Animation Test";
    public override    string Category    => "Animation";
    protected override string UdtFileName => "tests.animation.animation_test.xml";

    protected override void OnDocumentLoaded()
    {
        Document!.RootNode!.QuerySelector(".box")!.OnClick += _ => { };
    }
}