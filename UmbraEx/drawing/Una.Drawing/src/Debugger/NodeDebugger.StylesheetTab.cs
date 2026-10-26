
using System.Linq;

namespace Una.Drawing.Debugger;

internal partial class NodeDebugger
{
    private static void RenderStylesheetTab()
    {
        HoveredQuery = null;
        
        if (null == SelectedNode?.Stylesheet) return;
        
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(4, 4));
        ImGui.BeginChild("StylesheetViewer", new(0, 0), true, ImGuiWindowFlags.HorizontalScrollbar);

        int index = 0;

        foreach ((string key, Style style) in SelectedNode.Stylesheet.GetRuleList()) {
            index++;
            if (ImGui.CollapsingHeader($"{key}##{index}", ImGuiTreeNodeFlags.SpanFullWidth)) {
                if (ImGui.IsItemHovered()) HoveredQuery = key;
                
                ImGui.Indent();

                ImGui.BeginTable($"##styleTable_{key}_{index}", 2, ImGuiTableFlags.RowBg);
                ImGui.TableSetupColumn("Property");
                ImGui.TableSetupColumn("Value");

                var properties = style
                                .GetType().GetProperties()
                                .Where(p => p is { CanRead: true, CanWrite: true } && p.GetValue(style) != null)
                                .ToList();

                foreach (var property in properties) {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"  {property.Name}");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(property.GetValue(style)?.ToString() ?? "null");
                }

                ImGui.EndTable();

                ImGui.Unindent();
            }
            
            if (ImGui.IsItemHovered()) HoveredQuery = key;
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
    }
}