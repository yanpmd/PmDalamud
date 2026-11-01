using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Mappy.Classes.MapWindowComponents;

/// <summary>
/// Input box floating over the map itself, replacing the separate map selection window.
/// Type a part of a map name, the closest matches are listed right below and picking one shows that map.
/// </summary>
public class MapSearchOverlay
{
    private const int MaxResults = 8;

    private readonly List<MapIndex.SearchEntry> results = [];

    private string query = string.Empty;
    private string lastQuery = string.Empty;
    private int highlightedIndex;
    private bool focusInput;

    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        if (IsOpen) {
            Close();
        }
        else {
            Open();
        }
    }

    public void Open()
    {
        IsOpen = true;
        focusInput = true;
    }

    public void Close()
    {
        IsOpen = false;
        focusInput = false;
        query = string.Empty;
        lastQuery = string.Empty;
        highlightedIndex = 0;
        results.Clear();
    }

    public void Draw()
    {
        if (!IsOpen) return;

        RefreshResults();

        // Measure from the top of the map, whatever was drawn before this left the cursor somewhere else.
        ImGui.SetCursorPos(Vector2.Zero);

        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail();
        var width = MathF.Min(available.X - 24.0f * scale, 460.0f * scale);
        if (width < 180.0f * scale) return;

        var rowHeight = ImGui.GetTextLineHeight() + 6.0f * scale;
        var reservedRows = Math.Min(results.Count, MaxResults);
        var top = 38.0f * scale;
        var height = 34.0f * scale + reservedRows * rowHeight + 8.0f * scale;

        if (top + height > available.Y) height = MathF.Max(available.Y - top, 40.0f * scale);

        ImGui.SetCursorPos(new Vector2((available.X - width) / 2.0f, top));

        using var background = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.0f, 0.0f, 0.0f, 0.80f));
        using var panel = ImRaii.Child("map_search_panel", new Vector2(width, height), true);
        if (!panel) return;

        DrawInput();

        // Typing rebuilds the result list, so the amount of rows has to be read again after the input box.
        var visibleResults = Math.Min(results.Count, MaxResults);
        if (highlightedIndex >= visibleResults) highlightedIndex = Math.Max(visibleResults - 1, 0);

        DrawResults(rowHeight, visibleResults);
        HandleKeyboard(visibleResults);
    }

    private void DrawInput()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

        if (focusInput) {
            ImGui.SetKeyboardFocusHere();
            focusInput = false;
        }

        var hint = Localization.Get("MapSearch.Placeholder", "Search for a map...");

        if (ImGui.InputTextWithHint("##map_search_input", hint, ref query, 128, ImGuiInputTextFlags.AutoSelectAll)) {
            MapIndex.Search(query, results);
            lastQuery = query;
            highlightedIndex = 0;
        }
    }

    private void DrawResults(float rowHeight, int visibleResults)
    {
        if (visibleResults is 0) {
            if (query.Length is not 0) {
                ImGui.TextDisabled(Localization.Get("KamiLib.Selection.NoResults", "No Results"));
            }

            return;
        }

        for (var index = 0; index < visibleResults; index++) {
            if (index >= results.Count) break;

            var entry = results[index];

            using var id = ImRaii.PushId(index);

            if (ImGui.Selectable($"{entry.Label}  —  {entry.Context}", index == highlightedIndex, ImGuiSelectableFlags.None, new Vector2(0.0f, rowHeight))) {
                Pick(entry);
            }

            if (ImGui.IsItemHovered()) {
                highlightedIndex = index;
                ImGui.SetTooltip(entry.Path);
            }
        }
    }

    private void HandleKeyboard(int visibleResults)
    {
        // Picking a result closes the box, the keys must not reach a list that is gone already.
        if (!IsOpen) return;

        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape)) {
            Close();

            return;
        }

        if (visibleResults is 0) return;

        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) highlightedIndex = (highlightedIndex + 1) % visibleResults;
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) highlightedIndex = (highlightedIndex + visibleResults - 1) % visibleResults;

        if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)) {
            if (highlightedIndex < results.Count) Pick(results[highlightedIndex]);
        }
    }

    private void RefreshResults()
    {
        if (string.Equals(lastQuery, query, StringComparison.Ordinal)) return;

        lastQuery = query;

        MapIndex.Search(query, results);

        highlightedIndex = 0;
    }

    private void Pick(MapIndex.SearchEntry entry)
    {
        MapNavigator.Show(entry.MapId, entry.Position);

        Close();
    }
}
