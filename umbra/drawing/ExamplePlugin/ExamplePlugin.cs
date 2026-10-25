using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ExamplePlugin.Tests;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using Una.Drawing;

namespace ExamplePlugin;

public sealed partial class ExamplePlugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface         _plugin;
    private readonly ICommandManager                 _cmdManager;
    private readonly Configuration                   _configuration;
    private readonly Dictionary<string, DrawingTest> _tests      = [];
    private readonly List<string>                    _categories = [];

    private string       _activeCategory = "";
    private DrawingTest? _activeTest;
    private Exception?   _activateException;

    public ExamplePlugin(IPluginLog logger, IDalamudPluginInterface plugin, ICommandManager commandManager)
    {
        DrawingLib.Setup(plugin);

        DebugLogger.Writer = logger;

        _plugin     = plugin;
        _cmdManager = commandManager;

        var config = plugin.GetPluginConfig();
        if (config is Configuration configuration) {
            _configuration = configuration;
        } else {
            _configuration = new Configuration();
            plugin.SavePluginConfig(_configuration);
        }

        Node.UseThreadedStyleComputation = true;
        Node.ScaleFactor                 = _configuration.ScaleFactor;

        InitializeTests();
        InitializeMeasurements();

        _plugin.UiBuilder.Draw += OnDraw;

        if (_configuration.LastTestName == null || !_tests.ContainsKey(_configuration.LastTestName)) {
            if (_tests.Count > 0) {
                ActivateTest(_tests.First().Value);
            }
        } else {
            if (_tests.TryGetValue(_configuration.LastTestName, out DrawingTest? test)) {
                ActivateTest(test);
            } else {
                ActivateTest(_tests.First().Value);
            }
        }
    }

    public void Dispose()
    {
        _plugin.UiBuilder.Draw -= OnDraw;
        _activeTest?.OnDeactivate();

        DrawingLib.Dispose();
    }

    private void OnDraw()
    {
        double deltaTime = _loopTimer!.Elapsed.TotalMilliseconds;
        UpdateFrameTime(deltaTime);
        _loopTimer.Restart();

        if (_peakTimer!.Elapsed.Seconds >= 2) {
            _peakTimer.Restart();
            _testPeakTime = 0;
        }

        // Grab a safe portion of the screen to draw in.
        Vector2 workSize       = ImGui.GetMainViewport().WorkSize;
        Vector2 safeAreaOffset = new(workSize.X / 2f, 16 + 300);
        Vector2 safeArea       = workSize - safeAreaOffset;
        Vector2 windowPos      = new Vector2((workSize.X / 2f) - 8, 8);

        ImGui.SetNextWindowSize(safeArea, ImGuiCond.Always);
        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xff1a100a);
        ImGui.PushStyleColor(ImGuiCol.Border, 0xFF595959);
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFDADEDE);
        ImGui.Begin("Una.Drawing Sandbox", ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoFocusOnAppearing);

        RenderToolbar();
        RenderTestTabs();
        RenderTestWorkspace();

        ImGui.End();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(3);
    }

    private static ImGuiWindowFlags TestFrameWindowFlags =>
        ImGuiWindowFlags.None
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoMouseInputs
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoBackground;
}