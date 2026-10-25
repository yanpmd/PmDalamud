
using System.Linq;
using System.Text;

namespace Una.Drawing.Debugger;

internal static partial class NodeDebugger
{
    private static Node?   RootNode     { get; set; }
    private static Node?   SelectedNode { get; set; }
    private static Node?   HoveredNode  { get; set; }
    private static string? HoveredQuery { get; set; }

    internal static void SetActiveNode(Node node)
    {
        RootNode     = node.RootNode;
        SelectedNode = node;
    }

    public static void Dispose()
    {
        RootNode     = null!;
        SelectedNode = null;
        HoveredNode  = null;
    }

    internal static void Render()
    {
        if ((RootNode == null || RootNode.IsDisposed) && Node.InMemoryNodes.Count > 0) {
            RootNode     = Node.InMemoryNodes.First(n => !n.IsDisposed);
            SelectedNode = RootNode;
        }
        
        bool isOpen = true;

        ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF212021);
        ImGui.PushStyleColor(ImGuiCol.Border, 0xFF202020);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, 0xFF212021);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, 0xFF212021);
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, 0xFF212021);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF212021);
        ImGui.PushStyleColor(ImGuiCol.TabActive, 0xFF3A393A);
        ImGui.PushStyleColor(ImGuiCol.Tab, 0xFF212021);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, 0xFF292829);
        ImGui.PushStyleColor(ImGuiCol.Header, 0xFF3A393A);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(6, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        ImGui.SetNextWindowSizeConstraints(new(950, 500), new(1200, 1000));
        ImGui.Begin("Node Debugger", ref isOpen,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        RenderNodeTree();
        ImGui.SameLine();
        RenderWorkspace();
        RenderStatusBar();

        ImGui.End();
        ImGui.PopStyleVar(11);
        ImGui.PopStyleColor(10);

        if (HoveredNode != null) {
            RenderBoundingBoxes(HoveredNode);
        }

        if (RootNode != null && SelectedNode != null && HoveredQuery != null) {
            QuerySelector? qs = QuerySelectorParser.Parse(HoveredQuery).FirstOrDefault();
            if (qs != null && qs.Matches(RootNode)) RenderBoundingBoxes(RootNode);
            
            foreach (var node in RootNode.QuerySelectorAll(HoveredQuery)) {
                RenderBoundingBoxes(node);
            }
        }

        if (!isOpen) {
            DrawingLib.ShowDebugWindow = false;
        }
    }

    private static string GetRootNodeTimings()
    {
        if (RootNode == null) return "Select a node to inspect.";
        
        StringBuilder sb = new();

        sb.Append($"Reflow: {RootNode.ReflowTime.ToString("F2")} ms \t");
        sb.Append($"Layout: {RootNode.LayoutTime.ToString("F2")} ms \t");
        sb.Append($"Draw: {RootNode.DrawTime.ToString("F2")} ms");

        return sb.ToString();
    }

    private static void RenderBoundingBoxes(Node node)
    {
        Rect cRect = node.Bounds.ContentRect;
        Rect pRect = node.Bounds.PaddingRect;
        Rect mRect = node.Bounds.MarginRect;

        ImGui.GetForegroundDrawList().AddRectFilled(cRect.TopLeft, cRect.BottomRight, 0x40FAA9A9);
        ImGui.GetForegroundDrawList().AddRect(mRect.TopLeft, mRect.BottomRight, 0xFFA5F7FA);
        ImGui.GetForegroundDrawList().AddRect(pRect.TopLeft, pRect.BottomRight, 0xFFA9FAA9);
        ImGui.GetForegroundDrawList().AddRect(cRect.TopLeft, cRect.BottomRight, 0xFFFAA9A9);
    }
}