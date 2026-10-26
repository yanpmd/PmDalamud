using ExamplePlugin.Tests;

namespace ExamplePlugin.Tests.Styling;

internal class IconTest : SimpleUdtTest
{
    public override    string Name        => "Game Icons";
    public override    string Category    => "Styling";
    protected override string UdtFileName => "tests.styling.icon_test.xml";

    protected override void OnDocumentLoaded()
    {
        foreach (var lc in Document!.RootNode!.QuerySelectorAll(".icon")) {
            lc.OnClick += _ => { };
        }
    }
}
