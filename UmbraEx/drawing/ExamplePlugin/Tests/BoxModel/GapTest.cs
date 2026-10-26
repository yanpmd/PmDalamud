namespace ExamplePlugin.Tests.BoxModel;

internal sealed class GapTest : SimpleUdtTest
{
    public override string Name     => "Gap";
    public override string Category => "Box Model";

    protected override string UdtFileName => "box_model.gap_test.xml";
}