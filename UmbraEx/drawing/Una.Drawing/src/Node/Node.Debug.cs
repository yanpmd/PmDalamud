
using Una.Drawing.Debugger;

namespace Una.Drawing;

public partial class Node
{
    public static bool DrawDebugBoundingBoxes       { get; set; }
    public static bool DrawDebugPaintAndReflowBoxes { get; set; }

    /// <summary>
    /// Returns a string representation of this node.
    /// </summary>
    public override string ToString()
    {
        string type    = GetType().Name;
        string id      = string.IsNullOrWhiteSpace(Id) ? "" : $"#{Id}";
        string classes = ClassList.Count == 0 ? "" : $".{string.Join(".", ClassList)}";
        string tags    = TagsList.Count == 0 ? "" : $":{string.Join(":", TagsList)}";

        return $"{type} {id}{classes}{tags}".Trim();
    }

    internal static List<Node> InMemoryNodes { get; } = [];

    /// <summary>
    /// Returns a string representation of the node tree.
    /// </summary>
    public string DumpTree()
    {
        return "Node tree:\n" + DumpNode(this, 0);

        string DumpNode(Node node, int depth)
        {
            string indent     = node.ParentNode == null ? "" : $"{new(' ', depth * 2)} - ";
            string nodeString = $"{indent}{node}";

            foreach (Node child in node.ChildNodes) {
                nodeString += $"\n{DumpNode(child, depth + 1)}";
            }

            return nodeString;
        }
    }

    /// <summary>
    /// Returns a string representation of the full path to this node.
    /// </summary>
    /// <returns></returns>
    public string GetFullNodePath()
    {
        List<string> breadcrumbs = [];
        Node?        current     = this;

        while (current != null) {
            breadcrumbs.Insert(0, current.ToString());
            current = current.ParentNode;
        }

        return string.Join(" > ", breadcrumbs);
    }

    private static void TrackNodeRef(Node node)
    {
        if (node.ParentNode != null) return;
        if (InMemoryNodes.Contains(node)) return;

        InMemoryNodes.Add(node);
        node.OnDispose += _ => InMemoryNodes.Remove(node);
    }

    /// <summary>
    /// Draws debug information for this node.
    /// </summary>
    private void DrawDebugBounds(ImDrawListPtr drawList)
    {
        if (!DrawDebugBoundingBoxes) return;

        if (Bounds.ContentRect.IsEmpty) return;
        if (Bounds.PaddingRect.IsEmpty) return;

        Vector2 mousePos = ImGui.GetMousePos();
        int     depth    = GetNodeDepth();

        if (!Bounds.PaddingRect.Contains(mousePos)) {
            Vector2 offset = new(1, 1);
            drawList.AddRectFilled(Bounds.ContentRect.TopLeft, Bounds.ContentRect.BottomRight, DebugColors[depth]);
            drawList.AddRect(Bounds.MarginRect.TopLeft, Bounds.MarginRect.BottomRight, 0xFF00FFFF);
            drawList.AddRect(Bounds.MarginRect.TopLeft + offset, Bounds.MarginRect.BottomRight - offset, 0xFF00FFFF);
            drawList.AddRect(Bounds.PaddingRect.TopLeft, Bounds.PaddingRect.BottomRight, 0xFFFF00FF);
            return;
        }

        drawList.AddRect(Bounds.PaddingRect.TopLeft, Bounds.PaddingRect.BottomRight, 0xC0FFFF00);
        drawList.AddRect(Bounds.ContentRect.TopLeft, Bounds.ContentRect.BottomRight, 0xFF00FF00);

        ImDrawListPtr dl       = ImGui.GetForegroundDrawList();
        string        text     = $"Layer #{depth}: MR = {Bounds.MarginRect}\tPR = {Bounds.PaddingRect}\tCR = {Bounds.ContentRect}\tMS: {Bounds.MarginSize}\tPS: {Bounds.PaddingSize}\tCS: {Bounds.ContentSize}";
        Vector2       textPos  = mousePos + new Vector2(32f, 32f + (depth * 16f));
        Vector2       textSize = ImGui.CalcTextSize(text);

        dl.AddRectFilled(textPos, textPos + textSize, 0xFF000000);
        dl.AddText(textPos + new Vector2(1, 1), 0xFF000000, text);
        dl.AddText(textPos, 0xFFFFFFFF, text);
    }

    private int GetNodeDepth()
    {
        int   depth   = 0;
        Node? current = ParentNode;

        while (current != null) {
            depth++;
            current = current.ParentNode;
        }

        return depth;
    }

    private static readonly Dictionary<int, uint> DebugColors = new() {
        { 0, 0x800000FF },
        { 1, 0x8000FF00 },
        { 2, 0x80FF0000 },
        { 3, 0x80FFFF00 },
        { 4, 0x8000FFFF },
        { 5, 0x80FF00FF },
        { 6, 0x80000000 },
        { 7, 0x80FFFFFF },
        { 8, 0x80808080 },
        { 9, 0x80808000 },
        { 10, 0x80800080 },
        { 11, 0x80008080 },
        { 12, 0x80000080 },
        { 13, 0x80800000 },
        { 14, 0x80008000 },
        { 15, 0x80000040 },
        { 16, 0x80404040 },
        { 17, 0x80404000 },
        { 18, 0x80400040 },
        { 19, 0x80004040 },
        { 20, 0x80004000 },
        { 21, 0x80000020 },
        { 22, 0x80202020 },
        { 23, 0x80202000 },
        { 24, 0x80200020 },
        { 25, 0x80002020 },
        { 26, 0x80002000 },
        { 27, 0x80000010 },
        { 28, 0x80101010 },
        { 29, 0x80101000 },
        { 30, 0x80100010 },
        { 31, 0x80001010 },
        { 32, 0x80001000 },
    };
}