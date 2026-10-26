using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Una.Drawing;

namespace ExamplePlugin.Tests.BoxModel;

internal class RootAnchorsTest : DrawingTest
{
    public override string Name     => "Root Anchors";
    public override string Category => "Box Model";

    private readonly Dictionary<Anchor.AnchorPoint, Node> _nodes = [];

    public override void OnActivate()
    {
        foreach (Anchor.AnchorPoint point in Enum.GetValues<Anchor.AnchorPoint>()) {
            _nodes.Add(point, CreateNodeFromFile("box_model.root_anchors_test.xml"));
        }
    }

    public override void OnDeactivate()
    {
        foreach (var node in _nodes.Values) {
            node.Dispose();
        }

        _nodes.Clear();
    }

    public override void RenderConfig()
    {
        ImGui.TextWrapped("Tests the behavior of positioning the root node with different anchor points.");
    }

    public override void RenderTest(ImDrawListPtr dl)
    {
        dl.AddCircle(new Vector2(100, 100), 16, 0xFFFFFFFF, 8);

        RenderAnchorNode(dl, Anchor.AnchorPoint.TopLeft, new(0, 0));
        RenderAnchorNode(dl, Anchor.AnchorPoint.TopCenter, new(200, 0));
        RenderAnchorNode(dl, Anchor.AnchorPoint.TopRight, new(400, 0));

        RenderAnchorNode(dl, Anchor.AnchorPoint.MiddleLeft, new(0, 200));
        RenderAnchorNode(dl, Anchor.AnchorPoint.MiddleCenter, new(200, 200));
        RenderAnchorNode(dl, Anchor.AnchorPoint.MiddleRight, new(400, 200));

        RenderAnchorNode(dl, Anchor.AnchorPoint.BottomLeft, new(0, 400));
        RenderAnchorNode(dl, Anchor.AnchorPoint.BottomCenter, new(200, 400));
        RenderAnchorNode(dl, Anchor.AnchorPoint.BottomRight, new(400, 400));
    }

    private void RenderAnchorNode(ImDrawListPtr dl, Anchor.AnchorPoint point, Vector2 position)
    {
        Vector2 offset = new(10, 10);

        Node node = _nodes[point];
        node.Style.Anchor = point;
        node.NodeValue    = point.ToString();

        // No need to add padding to the position, because the ImGui child already has WindowPadding of 10.
        node.Render(dl, position);

        dl.AddCircleFilled(ImGui.GetWindowPos() + position + offset, 4, 0xFF0000FF);
    }
}