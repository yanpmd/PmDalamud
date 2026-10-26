using Dalamud.Bindings.ImGui;

namespace ExamplePlugin.Tests.Interaction;

internal class ButtonTest : SimpleUdtTest
{
    public override    string Name        => "Buttons";
    public override    string Category    => "Interaction";
    protected override string UdtFileName => "interaction.button_test.xml";

    private int _leftClickCount;
    private int _middleClickCount;
    private int _rightClickCount;

    protected override void OnDocumentLoaded()
    {
        foreach (var btn in Document!.RootNode!.QuerySelectorAll(".button")) {
            btn.OnClick       += _ => _leftClickCount++;
            btn.OnMiddleClick += _ => _middleClickCount++;
            btn.OnRightClick  += _ => _rightClickCount++;
        }

        foreach (var lc in Document!.RootNode!.QuerySelectorAll(".left-click")) {
            lc.OnClick += _ => _leftClickCount++;
        }

        foreach (var rc in Document!.RootNode!.QuerySelectorAll(".middle-click")) {
            rc.OnMiddleClick += _ => _middleClickCount++;
        }

        foreach (var rc in Document!.RootNode!.QuerySelectorAll(".right-click")) {
            rc.OnRightClick += _ => _rightClickCount++;
        }
    }

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped(
            "Tests the functionality of clickable nodes.\n\nUse the Node Tree inspector to see the interaction variables change as you hover the node.\n\nNOTE: Nodes that do NOT have a primary-action (meaning left click), do NOT receive the ':hover' tag."
        );
    }

    protected override void OnRenderTest(ImDrawListPtr dl)
    {
        Document!.RootNode!.QuerySelector("#left-click-counter")!.NodeValue =
            $"Left-clicked {_leftClickCount.ToString()} time(s).";
        Document!.RootNode!.QuerySelector("#middle-click-counter")!.NodeValue =
            $"Middle-clicked {_middleClickCount.ToString()} time(s).";
        Document!.RootNode!.QuerySelector("#right-click-counter")!.NodeValue =
            $"Right-clicked {_rightClickCount.ToString()} time(s).";
    }
}