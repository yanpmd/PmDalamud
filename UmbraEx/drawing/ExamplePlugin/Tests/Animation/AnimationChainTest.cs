using System;

namespace ExamplePlugin.Tests.Animation;

internal class AnimationChainTest : SimpleUdtTest
{
    public override    string Name        => "Animation Chain Test";
    public override    string Category    => "Animation";
    protected override string UdtFileName => "tests.animation.animation_chain_test.xml";

    protected override void OnDocumentLoaded()
    {
        if (Document == null) {
            throw new Exception("Document is NULL!");
        }

        if (Document.RootNode == null) {
            throw new Exception("RootNode is NULL!");
        }
        
        Document.RootNode.QuerySelector("#main")!.OnClick += n => n.ToggleClass("keyframe1", true);
    }
}