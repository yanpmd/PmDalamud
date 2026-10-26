using Dalamud.Game.Text.SeStringHandling;

namespace ExamplePlugin.Tests.BoxModel;

internal sealed class SeStringTest : SimpleUdtTest
{
    public override string Name     => "SeString";
    public override string Category => "Text";

    protected override string UdtFileName => "text.se_string_test.xml";

    protected override void OnDocumentLoaded()
    {
        SeStringBuilder builder = new();
        
        builder.AddText("Hello, ");
        builder.AddText("World!");
        builder.AddIcon(BitmapFontIcon.CrossWorld);
        builder.AddItalicsOn();
        builder.AddText("This is a test.");
        builder.AddItalicsOff();
        builder.AddIcon(BitmapFontIcon.Bard);
        builder.AddText("Bard");
        
        SeString str = builder.Build();
        
        Document!.RootNode!.QuerySelector("#SeString")!.NodeValue = str;
        Document!.RootNode!.QuerySelector("#String")!.NodeValue   = str.ToString();
    }
}