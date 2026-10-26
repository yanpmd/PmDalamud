namespace ExamplePlugin.Tests.BoxModel;

internal sealed class TextAlignmentTest : SimpleUdtTest
{
    public override string Name     => "Alignment";
    public override string Category => "Text";

    protected override string UdtFileName => "text.text_alignment_test.xml";
}