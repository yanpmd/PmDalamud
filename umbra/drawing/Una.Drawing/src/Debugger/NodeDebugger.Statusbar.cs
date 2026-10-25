using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;


namespace Una.Drawing.Debugger;

internal partial class NodeDebugger
{
    private static void RenderStatusBar()
    {
        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 30);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF212021);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 0));
        ImGui.BeginChild("Statusbar", new(-1, 29), false, ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings);
        ImGui.AlignTextToFramePadding();
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFACACAC);
        ImGui.TextUnformatted(GetRootNodeTimings());
        ImGui.SameLine();
        
        if (RootNode != null) {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 2));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4);
            ImGui.PushStyleColor(ImGuiCol.Button, 0xFF3A393A);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3F3E3F);
            
            string label = "Force reflow";
            float width = ImGui.CalcTextSize(label).X + 16;

            ImGui.SetNextItemWidth(width);
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - width - 4);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
            
            if (ImGui.Button(label)) {
                RootNode.Reflow();
            }
            
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }

        ImGui.PopStyleColor(1);
        ImGui.EndChild();
        ImGui.PopStyleVar(1);
        ImGui.PopStyleColor(1);
        
        Vector2 p1 = ImGui.GetWindowPos() + new Vector2(0, ImGui.GetWindowHeight() - 30);
        Vector2 p2 = ImGui.GetWindowPos() + new Vector2(ImGui.GetWindowWidth(), ImGui.GetWindowHeight() - 30);
        Vector2 o  = new(0, 1);
        
        ImGui.GetWindowDrawList().AddLine(p1, p2, 0xFF3A393A, 1f);
        ImGui.GetWindowDrawList().AddLine(p1 + o, p2 + o, 0xFF141414, 1f);
    }
}