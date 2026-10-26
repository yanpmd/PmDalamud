
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Una.Drawing.Debugger;

internal static partial class NodeDebugger
{
    private static bool _mRectHovered;
    private static bool _pRectHovered;
    private static bool _cRectHovered;

    private static void RenderLayoutTab()
    {
        RenderBoundingBox();

        ImGui.Dummy(new(0, 8));

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(12, 6));
        ImGui.BeginChild("LayoutSnapshotTableView", new(-1, -1), true, ImGuiWindowFlags.AlwaysUseWindowPadding);
        ImGui.BeginTable("LayoutSnapshotTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);

        ImGui.TableSetupColumn("Property");
        ImGui.TableSetupColumn("Value");
        ImGui.TableHeadersRow();

        foreach (var property in typeof(ComputedStyle).GetFields(BindingFlags.Instance | BindingFlags.Public)) {
            string? value = FormatObjectValue(property.GetValue(SelectedNode!.ComputedStyle));
            if (value == null) continue;
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(property.Name);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(value);
        }

        foreach (var property in typeof(ComputedStyle).GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
            string? value = FormatObjectValue(property.GetValue(SelectedNode!.ComputedStyle));
            if (value == null) continue;
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(property.Name);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(value);
        }

        ImGui.EndTable();
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
    }

    private static string? FormatObjectValue(object? value)
    {
        return value switch {
            null                          => null,
            Vector2 v                     => $"{v.X} x {v.Y}",
            Size s                        => $"{s.Width}, {s.Height}",
            Point p                       => $"{p.X}, {p.Y}",
            EdgeSize es                   => $"EdgeSize<{es.Top}, {es.Left}, {es.Right}, {es.Bottom}>",
            Rect r                        => $"Rect<{FormatObjectValue(r.TopLeft)}, {FormatObjectValue(r.BottomRight)}>",
            NodeBounds nb                 => $"NodeBounds:\n\tContentSize: {FormatObjectValue(nb.ContentSize)}\n\tPaddingSize: {FormatObjectValue(nb.PaddingSize)}\n\tMarginSize: {FormatObjectValue(nb.MarginSize)}\n\tContentRect: {FormatObjectValue(nb.ContentRect)}\n\tPaddingRect: {FormatObjectValue(nb.PaddingRect)}\n\tMarginRect: {FormatObjectValue(nb.MarginRect)}",
            Color c                       => $"Color<(#{c.ToUInt():x8})>",
            string str                    => str,
            bool b                        => b.ToString(),
            float f                       => f.ToString(CultureInfo.InvariantCulture),
            int i                         => i.ToString(CultureInfo.InvariantCulture),
            double d                      => d.ToString(CultureInfo.InvariantCulture),
            decimal m                     => m.ToString(CultureInfo.InvariantCulture),
            ObservableHashSet<string> set => string.Join(", ", set),
            IEnumerable enumerable        => string.Join(", ", enumerable.Cast<object>().Select(FormatObjectValue)),
            _                             => value.ToString()
        };
    }

    private static void RenderBoundingBox()
    {
        EdgeSize padding = SelectedNode!.ComputedStyle.Padding;
        EdgeSize margin  = SelectedNode.ComputedStyle.Margin;
        Rect     cRect   = SelectedNode.Bounds.ContentRect;
        Rect     pRect   = SelectedNode.Bounds.PaddingRect;
        Rect     mRect   = SelectedNode.Bounds.MarginRect;

        string pTop    = padding.Top == 0 ? "-" : padding.Top.ToString(CultureInfo.InvariantCulture);
        string pLeft   = padding.Left == 0 ? "-" : padding.Left.ToString(CultureInfo.InvariantCulture);
        string pRight  = padding.Right == 0 ? "-" : padding.Right.ToString(CultureInfo.InvariantCulture);
        string pBottom = padding.Bottom == 0 ? "-" : padding.Bottom.ToString(CultureInfo.InvariantCulture);
        string mTop    = margin.Top == 0 ? "-" : margin.Top.ToString(CultureInfo.InvariantCulture);
        string mLeft   = margin.Left == 0 ? "-" : margin.Left.ToString(CultureInfo.InvariantCulture);
        string mRight  = margin.Right == 0 ? "-" : margin.Right.ToString(CultureInfo.InvariantCulture);
        string mBottom = margin.Bottom == 0 ? "-" : margin.Bottom.ToString(CultureInfo.InvariantCulture);

        bool mEnabled = margin.Top != 0 || margin.Left != 0 || margin.Right != 0 || margin.Bottom != 0;
        bool pEnabled = padding.Top != 0 || padding.Left != 0 || padding.Right != 0 || padding.Bottom != 0;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(64, 24));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF212021);
        ImGui.BeginChild("BoundingBox", new(-1, 200), true, ImGuiWindowFlags.AlwaysUseWindowPadding);

        ImGui.SetCursorPosX((ImGui.GetWindowWidth() / 2f) - 200);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, _mRectHovered ? 0x50A5F7FA : 0xFF25373A);
        ImGui.BeginChild("MarginBox", new(400, -1), true, ImGuiWindowFlags.AlwaysUseWindowPadding);

        PrintTextAligned(mTop, new(0.5f, 0.04f));
        PrintTextAligned(mBottom, new(0.5f, 0.96f));
        PrintTextAligned(mLeft, new(0.1f, 0.5f));
        PrintTextAligned(mRight, new(0.9f, 0.5f));

        if (mEnabled && ImGui.IsWindowHovered()) {
            _mRectHovered = true;
            ImGui.GetForegroundDrawList().AddRectFilled(mRect.TopLeft, mRect.BottomRight, 0x50A5F7FA);
            ImGui.GetForegroundDrawList().AddRect(mRect.TopLeft, mRect.BottomRight, 0xFFA5F7FA);
        } else {
            _mRectHovered = false;
        }

        ImGui.PushStyleColor(ImGuiCol.ChildBg, _pRectHovered ? 0x50A9FAA9 : 0xFF495A49);
        ImGui.BeginChild("PaddingBox", new(-1, -1), true, ImGuiWindowFlags.AlwaysUseWindowPadding);

        PrintTextAligned(pTop, new(0.5f, 0.05f));
        PrintTextAligned(pBottom, new(0.5f, 0.94f));
        PrintTextAligned(pLeft, new(0.13f, 0.5f));
        PrintTextAligned(pRight, new(0.87f, 0.5f));

        if (pEnabled && ImGui.IsWindowHovered()) {
            ImGui.GetForegroundDrawList().AddRectFilled(pRect.TopLeft, pRect.BottomRight, 0x50A9FAA9);
            ImGui.GetForegroundDrawList().AddRect(mRect.TopLeft, mRect.BottomRight, 0xFFA5F7FA);
            ImGui.GetForegroundDrawList().AddRect(pRect.TopLeft, pRect.BottomRight, 0xFFA9FAA9);
            _pRectHovered = true;
        } else {
            _pRectHovered = false;
        }

        ImGui.PushStyleColor(ImGuiCol.ChildBg, _cRectHovered ? 0x50FAA9A9 : 0xFF5A4949);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.BeginChild("ContentBox", new(-1, -1), true, ImGuiWindowFlags.AlwaysUseWindowPadding);

        PrintTextAligned(SelectedNode!.Bounds.ContentSize.ToString(), new(0.5f, 0.5f));
        if (ImGui.IsWindowHovered()) {
            ImGui.GetForegroundDrawList().AddRectFilled(cRect.TopLeft, cRect.BottomRight, 0x50FAA9A9);
            ImGui.GetForegroundDrawList().AddRect(mRect.TopLeft, mRect.BottomRight, 0xFFA5F7FA);
            ImGui.GetForegroundDrawList().AddRect(pRect.TopLeft, pRect.BottomRight, 0xFFA9FAA9);
            ImGui.GetForegroundDrawList().AddRect(cRect.TopLeft, cRect.BottomRight, 0xFFFAA9A9);
            _cRectHovered = true;
        } else {
            _cRectHovered = false;
        }

        ImGui.EndChild(); // ContentBox
        ImGui.PopStyleVar(1);
        ImGui.PopStyleColor(1);

        ImGui.EndChild(); // PaddingBox
        ImGui.PopStyleColor(1);

        ImGui.EndChild(); // MarginBox
        ImGui.PopStyleColor(1);

        ImGui.EndChild(); // BoundingBox
        ImGui.PopStyleColor(1);
        ImGui.PopStyleVar();
    }

    private static void PrintTextAligned(string text, Vector2 align)
    {
        Vector2 textSize = ImGui.CalcTextSize(text);
        Vector2 pos = ImGui.GetWindowPos() + new Vector2(
            (ImGui.GetWindowWidth() - textSize.X) * align.X,
            (ImGui.GetWindowHeight() - textSize.Y) * align.Y
        );

        ImGui.GetWindowDrawList().AddText(pos, 0xFFFFFFFF, text);
    }
}