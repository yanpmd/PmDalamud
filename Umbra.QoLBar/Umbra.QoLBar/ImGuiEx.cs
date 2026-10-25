using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Umbra.Common;

namespace Umbra.QoLBar;

/// <summary>
/// Small ImGui helpers ported from QoLBarCN's ImGuiExtensions.
/// </summary>
public static class ImGuiEx
{
    public static void SetItemTooltip(string s, ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (!ImGui.IsItemHovered(flags)) return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
        ImGui.TextUnformatted(s);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static string search = string.Empty;
    private static HashSet<uint> filtered;

    /// <summary>
    /// A combo box over an Excel sheet with a search field.
    /// </summary>
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview, ImGuiComboFlags flags, Func<T, string, bool> searchPredicate, Func<T, bool> drawRow) where T : struct, IExcelRow<T>
    {
        selected = null;
        if (!ImGui.BeginCombo(id, preview, flags)) return false;

        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            search = string.Empty;
            filtered = null;
            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputText("##ExcelSheetComboSearch", ref search, 128))
            filtered = null;

        var sheet = Framework.Service<IDataManager>().GetExcelSheet<T>();
        filtered ??= sheet.Where(s => searchPredicate(s, search)).Select(s => s.RowId).ToHashSet();

        var i = 0;
        foreach (var rowID in filtered)
        {
            if (sheet.GetRowOrDefault(rowID) is not { } row) continue;

            ImGui.PushID(i++);
            if (drawRow(row))
                selected = row;
            ImGui.PopID();

            if (selected == null) continue;
            ImGui.EndCombo();
            return true;
        }

        ImGui.EndCombo();
        return false;
    }

    /// <summary>
    /// Draws a help marker that shows a tooltip when hovered.
    /// </summary>
    public static void HelpMarker(string desc)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }
}
