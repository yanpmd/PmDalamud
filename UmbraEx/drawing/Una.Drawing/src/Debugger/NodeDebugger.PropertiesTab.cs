
using System.Reflection;

namespace Una.Drawing.Debugger;

internal partial class NodeDebugger
{
    private static void RenderPropertiesTab()
    {
        bool isVisible  = SelectedNode!.ComputedStyle.IsVisible;
        bool isDisabled = SelectedNode.IsDisabled;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(16, 4));
        
        if (ImGui.Checkbox("Visible", ref isVisible)) SelectedNode.Style.IsVisible = isVisible;
        ImGui.SameLine();
        if (ImGui.Checkbox("Disabled", ref isDisabled)) SelectedNode.IsDisabled = isDisabled;
        ImGui.Dummy(new Vector2(0, 8));

        ImGui.BeginTable("NodeInfoTable", 2, ImGuiTableFlags.Borders);

        ImGui.TableSetupColumn("Property");
        ImGui.TableSetupColumn("Value");
        ImGui.TableHeadersRow();

        foreach (var property in SelectedNode.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
            if (property.Name is "Style" or "Stylesheet") continue;

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(property.Name);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(FormatObjectValue(property.GetValue(SelectedNode)) ?? "null");
        }

        ImGui.EndTable();
        ImGui.PopStyleVar(3);
    }
}