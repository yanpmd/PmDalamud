using ExamplePlugin.Tests;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Una.Drawing;
using Node = Una.Drawing.Node;

namespace ExamplePlugin;

public sealed partial class ExamplePlugin
{
    private void RenderTestWorkspace()
    {
        if (_activeTest == null) return;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 1);
        ImGui.BeginChild("Workspace", Vector2.Zero, false, ImGuiWindowFlags.AlwaysUseWindowPadding);
        RenderTestRenderer();
        ImGui.SameLine();
        RenderTestPanel();
        ImGui.EndChild(); // Workspace
    }

    private void RenderTestPanel()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF313031);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));
        ImGui.BeginChild("TestPanel", new Vector2(500, ImGui.GetWindowHeight() - 80), false, ImGuiWindowFlags.AlwaysUseWindowPadding);

        try {
            _activeTest?.RenderConfig();
        } catch (Exception ex) {
            ImGui.TextWrapped(ex.Message);
        }

        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(1);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF1F1F1F);
        ImGui.SetCursorPos(new(ImGui.GetWindowWidth() - 500, ImGui.GetWindowHeight() - 80));
        ImGui.BeginChild("GraphPanel", new Vector2(500, 80), false, ImGuiWindowFlags.AlwaysUseWindowPadding);

        Vector2 graphSize = new Vector2(ImGui.GetContentRegionAvail().X, 80);

        float max = _frameTimeHistory.Max();
        
        ImGui.PushStyleColor(ImGuiCol.PlotLines, max <= 10.0f ? 0xFF4D8B4D : 0xFF8B4D4D);
        
        /*
         public static void PlotLines(
           ImU8String label,
           ReadOnlySpan<float> values,
           int valuesOffset = 0,
           ImU8String overlayText = default,
           float scaleMin = float.MaxValue,
           float scaleMax = float.MaxValue,
           Vector2 graphSize = default,
           int stride = sizeof(float)
         )
         */
        
        ImGui.PlotLines(
            new ImU8String("##FrameTime"), // Unique ID
            new ReadOnlySpan<float>(_frameTimeHistory),
            _dataIndex,
            new ImU8String($"Frame time {max:F2} ms"),
            0.0f,
            30.0f,
            graphSize
        );
        ImGui.PopStyleColor(1);

        ImGui.EndChild();
        ImGui.PopStyleColor(1);
    }

    private void RenderTestRenderer()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));
        ImGui.BeginChild("TestRenderer", new Vector2(ImGui.GetContentRegionMax().X - 500, 0), false,
            ImGuiWindowFlags.AlwaysUseWindowPadding);
        RunActiveTest();
        ImGui.EndChild();
        ImGui.PopStyleVar(1);
    }

    #region Test Tabs

    private void RenderTestTabs()
    {
        string metrics       = GetMetricsString();
        float  metricsWidth  = ImGui.CalcTextSize(metrics).X + 8;
        float  tabFrameWidth = ImGui.GetContentRegionAvail().X - metricsWidth;

        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMin().Y + 40);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(16, 4));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF323132);
        ImGui.BeginChild("TestTabsFrame", new Vector2(tabFrameWidth, 39), false,
            ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoDecoration);

        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMin().Y + 8);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 8));
        ImGui.PushStyleColor(ImGuiCol.TabActive, ImGui.GetColorU32(ImGuiCol.WindowBg));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, ImGui.GetColorU32(ImGuiCol.WindowBg));
        ImGui.PushStyleColor(ImGuiCol.Tab, 0xFF292A2A);
        ImGui.BeginTabBar("TestTabs", ImGuiTabBarFlags.FittingPolicyScroll | ImGuiTabBarFlags.Reorderable);

        foreach (DrawingTest test in GetTestsFromCategory(_activeCategory)) {
            if (ImGui.BeginTabItem(test.Name)) {
                ActivateTest(test);
                ImGui.EndTabItem();
            }
        }

        ImGui.EndTabBar();
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(1);

        ImGui.EndChild();
        ImGui.PopStyleColor(1);
        ImGui.PopStyleVar(2);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF323132);
        ImGui.BeginChild("MetricsFrame", new Vector2(0, 40), false,
            ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoDecoration);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMin().Y + 2);
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFF9A9A9A);
        ImGui.TextUnformatted(metrics);
        ImGui.PopStyleColor(1);
        ImGui.EndChild();
        ImGui.PopStyleColor(1);
    }

    #endregion

    #region Toolbar

    private readonly Dictionary<string, string> _actionButtons = new() { { "Console", "/xllog" }, { "Stats", "/xlstats" }, { "Data", "/xldata" }, };

    private void RenderToolbar()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(12, 4));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF323132);
        ImGui.BeginChild("Toolbar", new(0, 40), false, ImGuiWindowFlags.AlwaysUseWindowPadding);

        RenderToolbarCategorySelector();
        ImGui.SameLine();

        RenderToolbarSeparator();
        ImGui.SameLine();

        RenderToolbarToggleButtons();
        ImGui.SameLine();

        RenderToolbarSeparator();
        RenderScaleFactorInput();
        RenderToolbarActionButtons();

        ImGui.EndChild();
        ImGui.PopStyleColor(1);
        ImGui.PopStyleVar(2);
    }

    private void RenderScaleFactorInput()
    {
        float scale = Node.ScaleFactor;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("UI Scale");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.DragFloat("##uiscale", ref scale, 0.01f, 0.1f, 4.0f)) {
            Node.ScaleFactor           = scale;
            _configuration.ScaleFactor = scale;
            _plugin.SavePluginConfig(_configuration);
        }
    }

    private static void RenderToolbarToggleButtons()
    {
        uint cButtonColor  = ImGui.GetColorU32(ImGuiCol.Button);
        uint cButtonColorH = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
        uint cButtonColorA = ImGui.GetColorU32(ImGuiCol.ButtonActive);

        uint aButtonColor  = 0xFF487940;
        uint aButtonColorH = 0xFF4D8B4D;
        uint aButtonColorA = 0xFF4D8B4D;

        ImGui.PushStyleColor(ImGuiCol.Button, Node.DrawDebugBoundingBoxes ? aButtonColor : cButtonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Node.DrawDebugBoundingBoxes ? aButtonColorH : cButtonColorH);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Node.DrawDebugBoundingBoxes ? aButtonColorA : cButtonColorA);
        if (ImGui.Button(Node.DrawDebugBoundingBoxes ? "Hide debug bounds" : "Show Debug Bounds"))
            Node.DrawDebugBoundingBoxes = !Node.DrawDebugBoundingBoxes;
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, DrawingLib.ShowDebugWindow ? aButtonColor : cButtonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, DrawingLib.ShowDebugWindow ? aButtonColorH : cButtonColorH);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, DrawingLib.ShowDebugWindow ? aButtonColorA : cButtonColorA);
        if (ImGui.Button(DrawingLib.ShowDebugWindow ? "Close Node Debugger" : "Open Node Debugger"))
            DrawingLib.ShowDebugWindow = !DrawingLib.ShowDebugWindow;
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Node.ScaleAffectsBorders ? aButtonColor : cButtonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Node.ScaleAffectsBorders ? aButtonColorH : cButtonColorH);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Node.ScaleAffectsBorders ? aButtonColorA : cButtonColorA);
        if (ImGui.Button(Node.ScaleAffectsBorders ? "Scale affects borders" : "Scale does not affect borders"))
            Node.ScaleAffectsBorders = !Node.ScaleAffectsBorders;
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Node.UseThreadedStyleComputation ? aButtonColor : cButtonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Node.UseThreadedStyleComputation ? aButtonColorH : cButtonColorH);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Node.UseThreadedStyleComputation ? aButtonColorA : cButtonColorA);
        if (ImGui.Button(Node.UseThreadedStyleComputation ? "Multithreading enabled" : "Multithreading disabled"))
            Node.UseThreadedStyleComputation = !Node.UseThreadedStyleComputation;
        ImGui.PopStyleColor(3);
    }

    private void RenderToolbarActionButtons()
    {
        ImGui.SameLine();
        float itemSpacing = ImGui.GetStyle().ItemSpacing.X;
        float width       = 0;

        foreach (string label in _actionButtons.Keys) {
            float buttonWidth = ImGui.CalcTextSize(label).X + itemSpacing * 2;
            width += buttonWidth;
        }

        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - width + itemSpacing - 8);
        foreach ((string label, string cmd) in _actionButtons) {
            if (ImGui.Button(label)) {
                _cmdManager.ProcessCommand(cmd);
            }

            ImGui.SameLine();
        }
    }

    private void RenderToolbarCategorySelector()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Category");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("##category", _activeCategory)) {
            foreach (string category in _categories) {
                if (ImGui.Selectable(category, category == _activeCategory)) {
                    _activeCategory = category;
                }
            }

            ImGui.EndCombo();
        }
    }

    private void RenderToolbarSeparator()
    {
        ImGui.SameLine();

        Vector2 pos    = ImGui.GetCursorScreenPos();
        Vector2 start1 = new Vector2(pos.X - 2, pos.Y + 2);
        Vector2 end1   = start1 with { Y = start1.Y + 20 };
        Vector2 start2 = new Vector2(pos.X - 1, pos.Y + 2);
        Vector2 end2   = start2 with { Y = start2.Y + 20 };

        ImGui.Dummy(Vector2.Zero);
        ImGui.SameLine();
        ImGui.GetWindowDrawList().AddLine(start1, end1, 0xFF212021);
        ImGui.GetWindowDrawList().AddLine(start2, end2, 0xFF595959);
    }

    #endregion
}