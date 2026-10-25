using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Umbra.Common;

namespace Umbra.QoLBar.UI;

/// <summary>
/// The second layer of a QoL Bar widget: a QoLBar-style icon bar drawn on top
/// of the game.
/// </summary>
/// <remarks>
/// Categories do not unfold inline. Clicking one opens an additional bar next
/// to the button it was opened from, so nested categories are laid out as
/// multiple stacked popups (like QoLBar does) instead of being flattened into
/// a single list.
/// </remarks>
public static class BarOverlay
{
    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoNavInputs
        | ImGuiWindowFlags.NoNavFocus
        | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.AlwaysAutoResize;

    private const uint DefaultButtonColor = 0xE6494949; // 0.286, 0.286, 0.286, 0.9

    /// <summary>
    /// One expanded category level. Level 0 is opened by a button of the main
    /// bar, level 1 by a button of level 0, and so on.
    /// </summary>
    private sealed class Level
    {
        public ShCfg   Category   = null!;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 LastSize;
    }

    private static readonly List<Level> Levels = [];

    private static bool    _isOpen;
    private static string? _barId;

    private static Vector2 _anchorMin;
    private static Vector2 _anchorMax;
    private static Vector2 _barSize;

    private static int      _suppressFrame = -1;
    private static int      _pendingDepth  = -1;
    private static ShCfg?   _pendingCategory;
    private static Vector2  _pendingMin;
    private static Vector2  _pendingMax;

    private static bool _closeRequested;

    public static bool IsOpen => _isOpen;

    /// <summary>
    /// Returns true if the overlay is currently showing the given bar.
    /// </summary>
    public static bool IsOpenFor(string barId) => _isOpen && _barId == barId;

    /// <summary>
    /// Opens the overlay for the given bar, or closes it when it is already
    /// showing that bar.
    /// </summary>
    public static void Toggle(string barId, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (_isOpen && _barId == barId) {
            Close();
            return;
        }

        _isOpen    = true;
        _barId     = barId;
        _barSize   = Vector2.Zero;
        _anchorMin = anchorMin;
        _anchorMax = anchorMax;

        Levels.Clear();

        // The click that opened the bar must not immediately close it again.
        _suppressFrame = ImGui.GetFrameCount();
    }

    /// <summary>
    /// Updates the anchor of the bar, so it follows its widget when the
    /// toolbar is moved.
    /// </summary>
    public static void UpdateAnchor(string barId, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (!IsOpenFor(barId)) return;

        _anchorMin = anchorMin;
        _anchorMax = anchorMax;
    }

    public static void Close()
    {
        _isOpen = false;
        _barId  = null;

        Levels.Clear();

        _pendingDepth    = -1;
        _pendingCategory = null;
        _closeRequested  = false;
        _barSize         = Vector2.Zero;
    }

    /// <summary>
    /// Draws the overlay. Invoked once per frame from the plugin's draw hook.
    /// </summary>
    public static void Draw()
    {
        if (!_isOpen) return;

        var manager = QoLBarManager.Instance;
        if (manager == null) {
            Close();
            return;
        }

        var bar = manager.GetBar(_barId);
        if (bar == null || !manager.IsBarVisible(bar)) {
            Close();
            return;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6, 5));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        bool hovered = false;

        try {
            hovered |= DrawMainBar(manager, bar);

            for (int i = 0; i < Levels.Count; i++) {
                hovered |= DrawCategoryBar(manager, bar, i);
            }
        } catch (Exception e) {
            Logger.Error($"QoL Bar overlay failed to render: {e.Message}");
            Close();
        } finally {
            ImGui.PopStyleVar(3);
        }

        ApplyPending();

        if (_closeRequested) {
            _closeRequested = false;
            Close();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape)) {
            Close();
            return;
        }

        if (_suppressFrame == ImGui.GetFrameCount()) return;

        // Clicking anywhere outside of the bars closes them.
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !hovered) Close();
    }

    #region Windows

    private static bool DrawMainBar(QoLBarManager manager, BarCfg bar)
    {
        var pos = ComputeMainPosition(_barSize);

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, GetSpacing(bar.Spacing));
        ImGui.PushStyleColor(ImGuiCol.Button, DefaultButtonColor);

        if (bar.NoBackground) ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);

        bool open = ImGui.Begin(
            $"###QoLBarBar_{_barId}",
            bar.NoBackground ? WindowFlags | ImGuiWindowFlags.NoBackground : WindowFlags
        );

        bool hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

        if (open) {
            hovered |= DrawShortcuts(manager, bar, bar.ShortcutList, 0, null);

            // QoLBar opens the bar configuration with a right click.
            if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right)) {
                manager.ToggleConfigWindow(_barId);
            }
        }

        _barSize = ImGui.GetWindowSize();

        ImGui.End();

        if (bar.NoBackground) ImGui.PopStyleColor();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        return hovered;
    }

    private static bool DrawCategoryBar(QoLBarManager manager, BarCfg bar, int index)
    {
        var level = Levels[index];
        var list  = level.Category.SubList;

        if (list == null || list.Count == 0) return false;

        bool noBackground = level.Category.CategoryNoBackground;
        var  pos          = ComputeCategoryPosition(level);

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, GetSpacing(level.Category.CategorySpacing));
        ImGui.PushStyleColor(ImGuiCol.Button, DefaultButtonColor);

        if (noBackground) ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);

        bool open = ImGui.Begin(
            $"###QoLBarCat_{_barId}_{index}",
            noBackground ? WindowFlags | ImGuiWindowFlags.NoBackground : WindowFlags
        );

        bool hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

        if (open) {
            hovered |= DrawShortcuts(manager, bar, list, index + 1, level.Category);
        }

        level.LastSize = ImGui.GetWindowSize();

        ImGui.End();

        if (noBackground) ImGui.PopStyleColor();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        return hovered;
    }

    private static Vector2 ComputeMainPosition(Vector2 size)
    {
        var vp = ImGui.GetMainViewport();

        float minX = vp.WorkPos.X + 8;
        float minY = vp.WorkPos.Y + 8;
        float maxX = vp.WorkPos.X + vp.WorkSize.X - 8;
        float maxY = vp.WorkPos.Y + vp.WorkSize.Y - 8;

        float x = _anchorMin.X;
        float y = _anchorMax.Y + 4;

        if (size.Y > 0 && y + size.Y > maxY) y = _anchorMin.Y - 4 - size.Y;
        if (size.X > 0 && x + size.X > maxX) x = maxX - size.X;

        x = Math.Clamp(x, minX, MathF.Max(minX, maxX - size.X));
        y = Math.Clamp(y, minY, MathF.Max(minY, maxY - size.Y));

        return new(x, y);
    }

    private static Vector2 ComputeCategoryPosition(Level level)
    {
        var vp = ImGui.GetMainViewport();

        float minX = vp.WorkPos.X + 8;
        float minY = vp.WorkPos.Y + 8;
        float maxX = vp.WorkPos.X + vp.WorkSize.X - 8;
        float maxY = vp.WorkPos.Y + vp.WorkSize.Y - 8;

        float w = level.LastSize.X;
        float h = level.LastSize.Y;

        // QoLBar pops categories out to the side of the button that opened them.
        float x = level.AnchorMax.X + 4;
        float y = level.AnchorMin.Y;

        if (w > 0 && x + w > maxX) x = level.AnchorMin.X - 4 - w;
        if (h > 0 && y + h > maxY) y = maxY - h;

        x = Math.Clamp(x, minX, MathF.Max(minX, maxX - w));
        y = Math.Clamp(y, minY, MathF.Max(minY, maxY - h));

        return new(x, y);
    }

    private static Vector2 GetSpacing(int[]? spacing)
    {
        if (spacing == null || spacing.Length < 2) return new(8, 4);

        return new(spacing[0], spacing[1]);
    }

    #endregion

    #region Shortcuts

    private static bool DrawShortcuts(
        QoLBarManager manager,
        BarCfg        bar,
        List<ShCfg>?  shortcuts,
        int           depth,
        ShCfg?        category
    )
    {
        if (shortcuts == null) return false;

        bool hovered = false;

        int columns = category != null ? category.CategoryColumns : bar.Columns;

        for (int i = 0; i < shortcuts.Count; i++) {
            ImGui.PushID(i);

            hovered |= DrawShortcut(manager, bar, shortcuts[i], depth, category);

            ImGui.PopID();

            if (columns <= 0 || i % columns != columns - 1) ImGui.SameLine();
        }

        return hovered;
    }

    private static bool DrawShortcut(
        QoLBarManager manager,
        BarCfg        bar,
        ShCfg         sh,
        int           depth,
        ShCfg?        category
    )
    {
        var name = sh.Name ?? string.Empty;
        bool hasIcon = IconManager.ParseName(
            ref name,
            out var subName,
            out var tooltip,
            out var icon,
            out var args
        );

        float fontSize = ImGui.GetFontSize();
        float height   = fontSize + ImGui.GetStyle().FramePadding.Y * 2;

        int rawWidth = category != null ? category.CategoryWidth : bar.ButtonWidth;
        float width  = MathF.Max(rawWidth * ImGuiHelpers.GlobalScale, height);

        if (sh.Type == ShCfg.ShortcutType.Spacer) {
            ImGui.Dummy(new Vector2(width, height));
            return false;
        }

        bool clicked;

        if (hasIcon) {
            var hint = subName;
            if (string.IsNullOrEmpty(hint)) hint = tooltip;

            clicked = DrawIconButton($"###icon", icon, new(height), sh, args, hint);
        } else {
            var label = string.IsNullOrEmpty(name) ? Loc.Get("Shortcut.UnnamedCategory") : name;

            clicked = DrawLabelButton(label, new(width, height), sh.Color, tooltip);
        }

        if (!clicked) return false;

        if (sh.Type == ShCfg.ShortcutType.Category && sh.Mode == ShCfg.ShortcutMode.Default) {
            // Remember where the newly expanded bar has to be anchored.
            _pendingDepth    = depth;
            _pendingCategory = sh;
            _pendingMin      = ImGui.GetItemRectMin();
            _pendingMax      = ImGui.GetItemRectMax();
        } else {
            manager.ExecuteShortcut(bar, sh);

            // Executing a shortcut closes the bar unless the user explicitly
            // asked categories to stay open.
            if (!sh.CategoryStaysOpen) _closeRequested = true;
        }

        return true;
    }

    private static bool DrawIconButton(string id, int icon, Vector2 size, ShCfg sh, string args, string tooltip)
    {
        bool clicked = ImGui.InvisibleButton(id, size);
        bool hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly);
        bool active  = ImGui.IsItemActive();

        var min      = ImGui.GetItemRectMin();
        var max      = min + size;
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(
            min,
            max,
            ImGui.GetColorU32(active ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)
        );

        var texture = icon == 0 ? null : GetTexture(icon, args);
        if (texture != null) {
            DrawIconTexture(
                texture,
                min + new Vector2(2),
                size - new Vector2(4),
                sh.IconZoom,
                new Vector2(sh.IconOffset[0], sh.IconOffset[1]),
                sh.IconRotation,
                args.Contains('r'),
                sh.Color
            );
        }

        drawList.AddRect(min, max, 0x40000000);

        DrawCooldown(drawList, min, size, sh);

        if (hovered && !string.IsNullOrEmpty(tooltip)) ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private static bool DrawLabelButton(string label, Vector2 size, uint color, string tooltip)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);

        bool clicked = ImGui.Button(SanitizeLabel(label), size);

        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(tooltip)) ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private static string SanitizeLabel(string label) => label.Replace("#", string.Empty);

    #endregion

    #region Icons

    private static IDalamudTextureWrap? GetTexture(int icon, string args)
    {
        var icons = IconManager.Instance;
        if (icons == null) return null;

        if (icon < 0) return icons.GetUserIconWrap(icon);

        bool useHr = QoLBarManager.Instance?.Config.Settings.UseHRIcons ?? false;
        if (args.Contains('h')) useHr = true;
        if (args.Contains('l')) useHr = false;

        return icons.GetGameIcon((uint)icon, useHr);
    }

    private static void DrawIconTexture(
        IDalamudTextureWrap texture,
        Vector2             pos,
        Vector2             size,
        float               zoom,
        Vector2             offset,
        float               rotation,
        bool                flipped,
        uint                color
    )
    {
        if (!IconManager.TryGetTexture(texture, out var validTexture)) return;

        var drawList = ImGui.GetWindowDrawList();

        float z = 0.5f / MathF.Max(zoom, 0.01f);

        var uv1 = new Vector2(0.5f - z + offset.X, 0.5f - z + offset.Y);
        var uv3 = new Vector2(0.5f + z + offset.X, 0.5f + z + offset.Y);

        var p1 = pos;
        var p2 = pos + new Vector2(size.X, 0);
        var p3 = pos + size;
        var p4 = pos + new Vector2(0, size.Y);

        float rCos = MathF.Cos(rotation);
        float rSin = -MathF.Sin(rotation);

        var uvHalfSize = (uv3 - uv1) / 2;
        var uvCenter   = uv1 + uvHalfSize;

        uv1 = uvCenter + RotateVector(-uvHalfSize, rCos, rSin);
        var uv2 = uvCenter + RotateVector(new Vector2(uvHalfSize.X, -uvHalfSize.Y), rCos, rSin);
        uv3 = uvCenter + RotateVector(uvHalfSize, rCos, rSin);
        var uv4 = uvCenter + RotateVector(new Vector2(-uvHalfSize.X, uvHalfSize.Y), rCos, rSin);

        if (flipped) {
            drawList.AddImageQuad(validTexture.Handle, p2, p1, p4, p3, uv1, uv2, uv3, uv4, color);
        } else {
            drawList.AddImageQuad(validTexture.Handle, p1, p2, p3, p4, uv1, uv2, uv3, uv4, color);
        }
    }

    private static Vector2 RotateVector(Vector2 v, float cos, float sin)
        => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

    private static void DrawCooldown(ImDrawListPtr drawList, Vector2 min, Vector2 size, ShCfg sh)
    {
        if (sh.CooldownAction == 0) return;

        float recast = GameActions.GetRecastTime(
            FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action,
            sh.CooldownAction
        );

        if (recast is <= 0 or > 3600) return;

        var text = recast >= 60
            ? $"{(int)recast / 60}:{(int)recast % 60:D2}"
            : $"{(int)MathF.Ceiling(recast)}";

        var textSize = ImGui.CalcTextSize(text);
        var pos      = new Vector2(
            min.X + (size.X - textSize.X) / 2,
            min.Y + size.Y - textSize.Y - 1
        );

        drawList.AddText(pos + new Vector2(1, 1), 0xFF000000, text);
        drawList.AddText(pos, 0xFFFFFFFF, text);
    }

    #endregion

    #region Expansion

    private static void ApplyPending()
    {
        if (_pendingCategory == null) return;

        int    depth    = _pendingDepth;
        var    category = _pendingCategory;
        var    min      = _pendingMin;
        var    max      = _pendingMax;
        _pendingCategory = null;
        _pendingDepth    = -1;

        // Clicking an already expanded category collapses it again.
        if (depth < Levels.Count && ReferenceEquals(Levels[depth].Category, category)) {
            Levels.RemoveRange(depth, Levels.Count - depth);
            return;
        }

        if (depth < Levels.Count) Levels.RemoveRange(depth, Levels.Count - depth);

        if (category.SubList == null || category.SubList.Count == 0) return;

        Levels.Add(
            new Level {
                Category  = category,
                AnchorMin = min,
                AnchorMax = max,
                LastSize  = Vector2.Zero,
            }
        );
    }

    #endregion
}
