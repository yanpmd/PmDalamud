using Dalamud.Game.Text;

using System.Linq;

namespace Una.Drawing.Debugger;

internal partial class NodeDebugger
{
    private static int _nodeTreeId;

    private static void RenderNodeTree()
    {
        _nodeTreeId = 0;
        HoveredNode = null;

        ImGui.BeginChild("SidePanel", new(300, ImGui.GetWindowHeight() - 60), false, ImGuiWindowFlags.NoScrollbar);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 0));
        ImGui.BeginChild("Toolbar", new(-1, 26), false, ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 2));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 2);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 4);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF3A393A);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, 0xFF3A393A);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0xFF3F3E3F);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3F3E3F);
        
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##SelectedRootNode", RootNode?.ToString() ?? "None", ImGuiComboFlags.HeightRegular)) {
            int index = 0;
            foreach (var node in Node.InMemoryNodes) {
                bool selected = ImGui.Selectable($"{node}##{index}", node == RootNode);
                if (ImGui.IsItemHovered()) RenderBoundingBoxes(node);                    
                if (selected) {
                    RootNode     = node;
                    SelectedNode = node;
                }

                index++;
            }

            ImGui.EndCombo();
        }

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(6);

        ImGui.EndChild(); // Toolbar
        ImGui.PopStyleVar();

        ImGui.Dummy(new(0, 1));
        
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 4));
        ImGui.BeginChild("NodeTree", new(300, -1), false,
            ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.HorizontalScrollbar
        );
        
        if (RootNode != null) RenderNodeTreeViewerNode(RootNode);
        
        ImGui.EndChild(); // NodeTree
        ImGui.PopStyleVar(2);
        
        ImGui.EndChild(); // SidePanel
    }

    private static void RenderNodeTreeViewerNode(Node node)
    {
        _nodeTreeId++;
        ImGui.PushStyleColor(ImGuiCol.Text, node.IsVisible ? 0xFFFFFFFF : 0xFF808080);
        ImGui.PushID(_nodeTreeId);

        ImGuiTreeNodeFlags nodeFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth;

        if (SelectedNode == node) nodeFlags |= ImGuiTreeNodeFlags.Selected;

        if (node.ChildNodes.Count == 0) {
            nodeFlags |= ImGuiTreeNodeFlags.Leaf;
            nodeFlags &= ~ImGuiTreeNodeFlags.OpenOnArrow;
        } else {
            nodeFlags |= ImGuiTreeNodeFlags.DefaultOpen;
        }

        bool nodeOpen = ImGui.TreeNodeEx($"{node}", nodeFlags);

        if (ImGui.IsItemClicked()) SelectedNode = node;
        if (ImGui.IsItemHovered()) HoveredNode  = node;

        if (nodeOpen) {
            foreach (var child in node.ChildNodes) RenderNodeTreeViewerNode(child);
            ImGui.TreePop();
        }

        ImGui.PopID();
        ImGui.PopStyleColor();
    }
}