using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace Umbra.QoLBar.UI;

/// <summary>
/// A small window for pasting an exported bar or condition set.
/// </summary>
public static class ImportWindow
{
    public static bool IsOpen { get; private set; }

    private static string _kind = "bar"; // "bar" | "set"
    private static string _buffer = string.Empty;

    public static void Open(string kind)
    {
        IsOpen = true;
        _kind = kind;
        _buffer = string.Empty;
    }

    public static void Close()
    {
        IsOpen = false;
        _buffer = string.Empty;
    }

    public static void Draw()
    {
        if (!IsOpen) return;

        var manager = QoLBarManager.Instance;

        var isOpen = true;
        ImGui.SetNextWindowSize(new Vector2(560, 320), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin(_kind == "bar" ? Loc.Get("ImportWindow.BarTitle") : Loc.Get("ImportWindow.SetTitle"), ref isOpen))
        {
            ImGui.End();
            if (!isOpen) Close();
            return;
        }

        ImGui.TextWrapped(Loc.Get("ImportWindow.Hint"));

        if (ImGui.InputTextMultiline("##ImportData", ref _buffer, 1_000_000, new Vector2(-1, 160)))
        {
        }

        if (ImGui.Button(Loc.Get("ImportWindow.Import")))
        {
            var success = _kind == "bar"
                ? manager.ImportBar(_buffer)
                : manager.ImportConditionSet(_buffer);

            if (success)
            {
                QoLBarManager.PrintEcho(Loc.Get("ImportWindow.Success"));
                Close();
            }
            else
            {
                QoLBarManager.PrintError(Loc.Get("ImportWindow.Failed"));
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("ImportWindow.Cancel")))
            Close();

        ImGui.End();

        if (!isOpen) Close();
    }
}
