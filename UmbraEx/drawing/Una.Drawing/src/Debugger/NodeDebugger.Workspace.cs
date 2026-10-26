

namespace Una.Drawing.Debugger;

internal partial class NodeDebugger
{
    private static WorkspaceTab _activeTab = WorkspaceTab.ComputedStyle;

    private static void RenderWorkspace()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF212021);
        
        ImGui.BeginChild("Workspace", new(-1, ImGui.GetWindowHeight() - 58), false,
            ImGuiWindowFlags.AlwaysUseWindowPadding);

        ImGui.BeginTabBar("WorkspaceTabs");

        foreach (string name in Enum.GetNames<WorkspaceTab>()) {
            if (name == "Stylesheet" && null == SelectedNode?.Stylesheet) continue;
            if (ImGui.BeginTabItem(name)) {
                _activeTab = Enum.Parse<WorkspaceTab>(name);
                ImGui.EndTabItem();
            }
        }

        ImGui.EndTabBar();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF3A393A);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));

        float width  = ImGui.GetWindowWidth();
        float height = ImGui.GetWindowHeight() - 30;

        ImGui.BeginChild("DebugTab", new(width, height), false, ImGuiWindowFlags.AlwaysUseWindowPadding);

        if (SelectedNode != null) {
            switch (_activeTab) {
                case WorkspaceTab.ComputedStyle:
                    RenderLayoutTab();
                    break;
                case WorkspaceTab.Properties:
                    RenderPropertiesTab();
                    break;
                case WorkspaceTab.Stylesheet:
                    RenderStylesheetTab();
                    break;
            }
        }

        ImGui.EndChild(); // DebugTab
        ImGui.PopStyleVar(1);
        ImGui.PopStyleColor();

        ImGui.EndChild(); // Workspace
        ImGui.PopStyleColor(1);
        ImGui.PopStyleVar(1);
    }

    private enum WorkspaceTab
    {
        Properties,
        ComputedStyle,
        Stylesheet,
    }
}