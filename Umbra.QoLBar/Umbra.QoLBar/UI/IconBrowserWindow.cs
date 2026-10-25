using System;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace Umbra.QoLBar.UI;

/// <summary>
/// A browser window for picking game icons or user icons.
/// </summary>
public static class IconBrowserWindow
{
    public static bool IsOpen { get; private set; }

    private static Action<int> _onPick;
    private static int _pageStart;
    private static bool _onlyExisting = true;
    private static bool _useHr;
    private static int _directId;

    private const int PerPage = 120;

    public static void Open(Action<int> onPick)
    {
        IsOpen = true;
        _onPick = onPick;
        _pageStart = 0;
        _directId = 0;
    }

    public static void Close()
    {
        IsOpen = false;
        _onPick = null;
    }

    public static void Draw()
    {
        if (!IsOpen) return;

        var manager = QoLBarManager.Instance;
        _useHr = manager?.Config.Settings.UseHRIcons ?? false;

        var isOpen = true;
        ImGui.SetNextWindowSize(new Vector2(640, 560), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin(Loc.Get("IconBrowser.Title"), ref isOpen))
        {
            ImGui.End();
            if (!isOpen) Close();
            return;
        }

        // Direct ID entry
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("##IconId", ref _directId))
        {
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("IconBrowser.Use")) && _directId > 0)
        {
            _onPick?.Invoke(_directId);
            Close();
        }

        ImGui.SameLine();
        ImGui.Checkbox(Loc.Get("IconBrowser.OnlyExisting"), ref _onlyExisting);

        ImGui.SameLine();
        ImGui.Checkbox(Loc.Get("IconBrowser.HighRes"), ref _useHr);

        // Page navigation
        ImGui.Separator();
        if (ImGui.Button("<<")) _pageStart = Math.Max(0, _pageStart - PerPage * 10);
        ImGui.SameLine();
        if (ImGui.Button("<")) _pageStart = Math.Max(0, _pageStart - PerPage);
        ImGui.SameLine();
        ImGui.Text($"{_pageStart} - {_pageStart + PerPage - 1}");
        ImGui.SameLine();
        if (ImGui.Button(">")) _pageStart += PerPage;
        ImGui.SameLine();
        if (ImGui.Button(">>")) _pageStart += PerPage * 10;

        ImGui.Separator();

        // User icons
        var userIcons = IconManager.Instance?.GetUserIcons();
        if (userIcons is { Count: > 0 })
        {
            ImGui.Text(Loc.Get("IconBrowser.Custom"));

            foreach (var (id, _) in userIcons)
            {
                ImGui.PushID(id);

                var wrap = IconManager.Instance?.GetUserIconWrap(id);
                if (IconManager.TryGetTexture(wrap, out var texture))
                {
                    ImGui.Image(texture.Handle, new Vector2(32, 32));
                    if (ImGui.IsItemClicked())
                    {
                        _onPick?.Invoke(id);
                        Close();
                        ImGui.PopID();
                        break;
                    }
                }
                else
                {
                    ImGui.Button($"{id}", new Vector2(32, 32));
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{id}");

                ImGui.PopID();
                ImGui.SameLine();
            }

            ImGui.NewLine();
            ImGui.Separator();
        }

        // Game icon grid
        ImGui.BeginChild("##IconGrid", new Vector2(-1, -1), true);

        var columns = 8;
        var drawn = 0;

        for (var i = 0; i < PerPage; i++)
        {
            var id = (uint)(_pageStart + i);
            if (id == 0) continue;

            if (_onlyExisting && !IconManager.IconExists(id)) continue;

            var wrap = IconManager.Instance?.GetGameIcon(id, _useHr);

            if (drawn > 0 && drawn % columns == 0) ImGui.NewLine();
            drawn++;

            ImGui.PushID((int)id);

            if (IconManager.TryGetTexture(wrap, out var texture))
            {
                ImGui.Image(texture.Handle, new Vector2(36, 36));
                if (ImGui.IsItemClicked())
                {
                    _onPick?.Invoke((int)id);
                    Close();
                    ImGui.PopID();
                    break;
                }
            }
            else
            {
                ImGui.Button("##empty", new Vector2(36, 36));
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{id}");

            ImGui.PopID();
            ImGui.SameLine();
        }

        ImGui.EndChild();

        ImGui.End();

        if (!isOpen) Close();
    }
}
