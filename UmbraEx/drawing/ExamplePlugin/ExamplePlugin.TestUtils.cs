using ExamplePlugin.Tests;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using Una.Drawing;

namespace ExamplePlugin;

public sealed partial class ExamplePlugin
{
    private void ActivateTest(DrawingTest test)
    {
        if (_activeTest == test) return;

        _activeTest?.OnDeactivate();

        _activeTest        = test;
        _activeCategory    = _activeTest.Category;
        _activateException = null;
        _testPeakTime      = 0;

        _peakTimer!.Restart();

        try {
            _activeTest = test;
            test.OnActivate();
        } catch (Exception ex) {
            _activateException = ex;
            DebugLogger.Error(ex.Message);
            DebugLogger.Error(ex.StackTrace ?? " - No stacktrace available -");
        }

        _configuration.LastTestName = test.GetType().Name;
        _plugin.SavePluginConfig(_configuration);
    }

    private void RunActiveTest()
    {
        if (null == _activeTest) return;
        
        try {
            _testTimer!.Restart();

            if (_activateException != null) {
                RenderException(_activateException);
            } else {
                _activeTest.RenderTest(ImGui.GetWindowDrawList());
            }
        } catch (Exception ex) {
            RenderException(ex);
        } finally {
            _testTime     = _loopTimer!.Elapsed.TotalMilliseconds;
            _testPeakTime = Math.Max(_testTime, _testPeakTime);
        }
    }

    private List<DrawingTest> GetTestsFromCategory(string category)
    {
        return _tests.Where(test => test.Value.Category == category)
                     .Select(test => test.Value)
                     .OrderBy(test => test.Name)
                     .ToList();
    }

    private static void RenderException(Exception ex)
    {
        ImGui.SetCursorPos(new(10, 10));
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFF0000FF);
        ImGui.TextUnformatted($"Error: {ex.Message}\n");
        ImGui.PopStyleColor();
        ImGui.SetCursorPosX(16);
        ImGui.TextUnformatted(ex.StackTrace);
    }
}