using Dalamud.Bindings.ImGui;
using System;
using Una.Drawing;

namespace ExamplePlugin.Tests.BoxModel;

internal sealed class AutoSizeTest : SimpleUdtTest
{
    public override string Name     => "Auto Size";
    public override string Category => "Box Model";

    protected override string UdtFileName => "box_model.auto_size_test.xml";

    private UldStyle _selectedUldStyle = UldStyle.Default;

    protected override void OnRenderConfig()
    {
        ImGui.TextWrapped("Tests the functionality of the auto-size style attribute.\n\nThe layout algorithm automatically expands nodes on either horizontal or vertical or both axis based on the available space in the parent node. The window drawn in this test consists of a typical 9-slice layout, which is a common pattern in UI design. The 9-slice layout allows for flexible resizing of the node while maintaining the integrity of the corners and edges.\n\nThe middle sections of the header and footer are set to grow horizontally, while the left and right portions of the 'body' section grow vertically.");
        ImGui.Separator();
        
        if (ImGui.BeginCombo("ULD Theme", _selectedUldStyle.ToString())) {
            foreach (UldStyle style in Enum.GetValues<UldStyle>()) {
                if (ImGui.MenuItem(style.ToString(), "", style == _selectedUldStyle)) {
                    _selectedUldStyle = style;
                }
            }
            
            ImGui.EndCombo();
        }
    }

    protected override void OnRenderTest(ImDrawListPtr dl)
    {
        if (Document?.RootNode is null) return;

        foreach (Node node in Document.RootNode.QuerySelectorAll(".uld")) {
            node.Style.UldStyle = _selectedUldStyle;
        }

        foreach (UldStyle style in Enum.GetValues<UldStyle>()) {
            Document.RootNode.ToggleClass(style.ToString().ToLowerInvariant(), _selectedUldStyle == style);
        }
    }
}