namespace ExamplePlugin.Tests.BoxModel;

internal sealed class TextTest : SimpleUdtTest
{
    public override string Name     => "Text";
    public override string Category => "Text";

    protected override string UdtFileName => "text.text_test.xml";
}