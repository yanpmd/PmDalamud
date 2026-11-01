using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Mappy.MapRenderer;

public unsafe partial class MapRenderer
{
    /// <summary>
    /// Every map is drawn as a 2048x2048 texture, which covers the whole map coordinate range.
    /// </summary>
    private const float MapTextureSize = 2048.0f;

    /// <summary>
    /// Map coordinates start at 1 in the upper left corner of the texture. The game derives them as
    /// 2 * texturePixel / sizeFactor + 1, so a single coordinate unit always covers sizeFactor / 2 pixels,
    /// which works out to exactly 50 yalms for every map.
    /// </summary>
    private const float CoordinateOrigin = 1.0f;

    /// <summary>
    /// Steps that read well on a grid, the smallest one that still has enough space on screen is used.
    /// </summary>
    private static readonly float[] GridSteps = [1.0f, 2.0f, 5.0f, 10.0f, 20.0f, 25.0f, 50.0f];

    private void DrawCoordinateGrid()
    {
        if (!System.SystemConfig.ShowCoordinateGrid) return;

        var sizeFactor = AgentMap.Instance()->SelectedMapSizeFactor;
        if (sizeFactor is 0) return;

        var pixelsPerUnit = sizeFactor / 2.0f;
        var coordinateSpan = 2.0f * MapTextureSize / sizeFactor;

        // DrawPosition is relative to the window we are drawing into, so this is where the texture starts on screen.
        var textureMin = ImGui.GetWindowPos() + DrawPosition;
        var textureMax = textureMin + new Vector2(MapTextureSize, MapTextureSize) * Scale;

        // Only the part inside the window is visible, the labels are placed along the edges of that part.
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();

        var clipMin = Vector2.Max(textureMin, windowMin);
        var clipMax = Vector2.Min(textureMax, windowMax);
        if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) return;

        var step = GetGridStep(pixelsPerUnit * Scale, coordinateSpan);
        var lineSpacing = step * pixelsPerUnit * Scale;
        if (lineSpacing <= 0.0f) return;

        var lineCount = (int)MathF.Floor(coordinateSpan / step);

        var drawList = ImGui.GetWindowDrawList();
        var gridColor = System.SystemConfig.CoordinateGridColor;
        var lineColor = ImGui.GetColorU32(gridColor);
        var majorLineColor = ImGui.GetColorU32(gridColor with { W = MathF.Min(1.0f, gridColor.W * 2.0f) });

        var firstColumn = Math.Clamp((int)MathF.Floor((clipMin.X - textureMin.X) / lineSpacing), 0, lineCount);
        var lastColumn = Math.Clamp((int)MathF.Ceiling((clipMax.X - textureMin.X) / lineSpacing), 0, lineCount);

        var firstRow = Math.Clamp((int)MathF.Floor((clipMin.Y - textureMin.Y) / lineSpacing), 0, lineCount);
        var lastRow = Math.Clamp((int)MathF.Ceiling((clipMax.Y - textureMin.Y) / lineSpacing), 0, lineCount);

        for (var index = firstColumn; index <= lastColumn; index++) {
            var x = textureMin.X + index * lineSpacing;
            var isMajor = index % 5 is 0;

            drawList.AddLine(new Vector2(x, clipMin.Y), new Vector2(x, clipMax.Y), isMajor ? majorLineColor : lineColor, isMajor ? 2.0f : 1.0f);
        }

        for (var index = firstRow; index <= lastRow; index++) {
            var y = textureMin.Y + index * lineSpacing;
            var isMajor = index % 5 is 0;

            drawList.AddLine(new Vector2(clipMin.X, y), new Vector2(clipMax.X, y), isMajor ? majorLineColor : lineColor, isMajor ? 2.0f : 1.0f);
        }

        DrawCoordinateGridLabels(firstColumn, lastColumn, firstRow, lastRow, step, lineSpacing, textureMin, clipMin);
    }

    /// <summary>
    /// Draws the numbers next to the grid lines, vertical lines are labeled along the top edge and
    /// horizontal lines along the left edge, so the labels stay visible while panning.
    /// </summary>
    private static void DrawCoordinateGridLabels(int firstColumn, int lastColumn, int firstRow, int lastRow, float step, float lineSpacing,
        Vector2 textureMin, Vector2 clipMin)
    {
        var cursorPosition = ImGui.GetCursorPos();
        var padding = 6.0f * ImGuiHelpers.GlobalScale;

        for (var index = firstColumn; index <= lastColumn; index++) {
            var x = textureMin.X + index * lineSpacing;
            DrawCoordinateGridLabel(new Vector2(x + padding, clipMin.Y + padding), index, step);
        }

        for (var index = firstRow; index <= lastRow; index++) {
            var y = textureMin.Y + index * lineSpacing;

            // Skip the label that would end up on top of the first column label.
            if (MathF.Abs(y - clipMin.Y) < ImGui.GetFontSize() * 1.5f) continue;

            DrawCoordinateGridLabel(new Vector2(clipMin.X + padding, y - ImGui.GetFontSize() / 2.0f), index, step);
        }

        ImGui.SetCursorPos(cursorPosition);
    }

    private static void DrawCoordinateGridLabel(Vector2 position, int index, float step)
    {
        var text = (CoordinateOrigin + index * step).ToString("0", CultureInfo.InvariantCulture);
        var textColor = System.SystemConfig.CoordinateTextColor;

        // Same as the map labels, a dark shadow keeps the numbers readable on bright map textures.
        ImGui.SetCursorScreenPos(position + Vector2.One);
        ImGui.TextColored(new Vector4(0.0f, 0.0f, 0.0f, textColor.W), text);

        ImGui.SetCursorScreenPos(position);
        ImGui.TextColored(textColor, text);
    }

    /// <summary>
    /// Picks the smallest step that is far enough apart on screen, without going coarser than the map
    /// allows, large maps only span a handful of coordinate units in total.
    /// </summary>
    private static float GetGridStep(float pixelsPerUnit, float coordinateSpan)
    {
        var minimumSpacing = 50.0f * ImGuiHelpers.GlobalScale;
        var maximumStep = MathF.Max(GridSteps[0], coordinateSpan / 3.0f);

        var fallback = GridSteps[0];

        foreach (var step in GridSteps) {
            if (step > maximumStep) break;

            fallback = step;

            if (step * pixelsPerUnit >= minimumSpacing) return step;
        }

        return fallback;
    }
}
