using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Umbra.QoLBar.UI;

/// <summary>
/// An editor window for a single shortcut. Categories are navigated into
/// using a path stack, so nested categories can be edited as well.
/// </summary>
public static class ShortcutEditorWindow
{
    public static bool IsOpen { get; private set; }

    private static string _barId;
    private static readonly List<ShCfg> _path = new();

    private static ShCfg _loadedTarget;

    public static void Open(string barId, ShCfg shortcut)
    {
        IsOpen = true;
        _barId = barId;
        _path.Clear();
        _path.Add(shortcut);
        _loadedTarget = shortcut;
    }

    public static void Close()
    {
        IsOpen = false;
        _path.Clear();
        _loadedTarget = null;
    }

    private static ShCfg Current => _path.Count > 0 ? _path[^1] : null;

    public static void Draw()
    {
        if (!IsOpen) return;

        var manager = QoLBarManager.Instance;
        var bar = manager?.GetBar(_barId);
        var target = Current;

        if (bar == null || target == null)
        {
            Close();
            return;
        }

        var isOpen = true;
        ImGui.SetNextWindowSize(new Vector2(520, 620), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin(Loc.Get("Editor.Title"), ref isOpen))
        {
            ImGui.End();
            if (!isOpen) Close();
            return;
        }

        // Breadcrumb
        ImGui.Text(bar.Name);

        for (var i = 0; i < _path.Count; i++)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(">");
            ImGui.SameLine();

            var label = _path[i].Name.Split("##", 2, StringSplitOptions.None)[0];
            ImGui.Text(string.IsNullOrEmpty(label) ? Loc.Get("Shortcut.UnnamedCategory") : label);
        }

        if (_path.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("◀ " + Loc.Get("Editor.Back")))
            {
                _path.RemoveAt(_path.Count - 1);
                _loadedTarget = Current;
            }
        }

        ImGui.Separator();

        // --- Name
        var name = target.Name;
        if (ImGui.InputText(Loc.Get("Editor.Name"), ref name, 256))
        {
            target.Name = name;
            manager.SaveConfig();
        }
        ImGuiEx.SetItemTooltip(Loc.Get("Editor.NameTooltip"));

        // --- Type
        var typeIndex = (int)target.Type;
        var typeNames = new[] { Loc.Get("Shortcut.TypeCommand"), Loc.Get("Shortcut.TypeCategory"), Loc.Get("Shortcut.TypeSpacer") };
        if (ImGui.Combo(Loc.Get("Editor.Type"), ref typeIndex, typeNames, typeNames.Length))
        {
            target.Type = (ShCfg.ShortcutType)typeIndex;
            if (target.Type == ShCfg.ShortcutType.Category && target.SubList == null)
                target.SubList = new List<ShCfg>();
            manager.SaveConfig();
        }

        if (target.Type == ShCfg.ShortcutType.Command)
        {
            ImGui.Text(Loc.Get("Editor.Command"));
            var command = target.Command;
            if (ImGui.InputTextMultiline("##Command", ref command, 65535, new Vector2(-1, 120)))
            {
                target.Command = command;
                manager.SaveConfig();
            }
            ImGuiEx.SetItemTooltip(Loc.Get("Editor.CommandTooltip"));
        }

        if (target.Type != ShCfg.ShortcutType.Spacer)
        {
            // --- Mode
            var modeIndex = (int)target.Mode;
            var modeNames = Enum.GetNames(typeof(ShCfg.ShortcutMode));
            if (ImGui.Combo(Loc.Get("Editor.Mode"), ref modeIndex, modeNames, modeNames.Length))
            {
                target.Mode = (ShCfg.ShortcutMode)modeIndex;
                manager.SaveConfig();
            }
            ImGuiEx.SetItemTooltip(Loc.Get("Editor.ModeTooltip"));

            // --- Color
            var color = ImGui.ColorConvertU32ToFloat4(target.Color);
            if (ImGui.ColorEdit4(Loc.Get("Editor.Color"), ref color, ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.AlphaPreviewHalf))
            {
                target.Color = ImGui.ColorConvertFloat4ToU32(color);
                manager.SaveConfig();
            }

            // --- Icon
            DrawIconEditor(manager, target);

            // --- Hotkey
            var hotkey = target.Hotkey;
            if (KeybindManager.InputHotkey("##ShHotkey", ref hotkey))
            {
                target.Hotkey = hotkey;
                manager.SaveConfig();
            }
            ImGui.SameLine();
            ImGui.Text(Loc.Get("Keybind.Hotkey"));

            if (target.Hotkey > 0)
            {
                var passthrough = target.KeyPassthrough;
                if (ImGui.Checkbox(Loc.Get("Keybind.PassInputToGame"), ref passthrough))
                {
                    target.KeyPassthrough = passthrough;
                    manager.SaveConfig();
                }
            }

            // --- Cooldown
            var cooldownAction = (int)target.CooldownAction;
            if (ImGui.InputInt(Loc.Get("Editor.CooldownAction"), ref cooldownAction))
            {
                target.CooldownAction = (uint)Math.Max(cooldownAction, 0);
                manager.SaveConfig();
            }
            ImGuiEx.SetItemTooltip(Loc.Get("Editor.CooldownActionTooltip"));
        }

        // --- Sub shortcuts (categories)
        if (target.Type == ShCfg.ShortcutType.Category)
        {
            ImGui.Separator();
            ImGui.Text(Loc.Get("Editor.SubShortcuts"));

            target.SubList ??= new List<ShCfg>();

            ImGui.BeginChild("##SubList", new Vector2(-1, 200), true);

            for (var i = 0; i < target.SubList.Count; i++)
            {
                var sub = target.SubList[i];
                ImGui.PushID(i);

                var subName = sub.Name;
                var hasIcon = IconManager.ParseName(ref subName, out var subSubName, out _, out _, out _);
                var display = sub.Type switch
                {
                    ShCfg.ShortcutType.Spacer => Loc.Get("Shortcut.TypeSpacer"),
                    ShCfg.ShortcutType.Category => string.IsNullOrEmpty(subName) ? Loc.Get("Shortcut.TypeCategory") : subName,
                    _ => hasIcon && !string.IsNullOrEmpty(subSubName) ? subSubName : (string.IsNullOrEmpty(subName) ? Loc.Get("Shortcut.TypeCommand") : subName)
                };

                ImGui.Button($"{display}##row", new Vector2(220, 0));
                if (ImGui.IsItemClicked())
                {
                    _path.Add(sub);
                    _loadedTarget = sub;
                }

                var buttonWidth = ImGui.GetFrameHeight() + 6;
                var totalWidth = buttonWidth * 3 + 4;
                ImGui.SameLine();
                ImGui.SetCursorPosX(MathF.Max(ImGui.GetWindowContentRegionMax().X - totalWidth, ImGui.GetCursorPosX()));

                if (ImGui.Button("▲") && i > 0)
                {
                    (target.SubList[i], target.SubList[i - 1]) = (target.SubList[i - 1], target.SubList[i]);
                    manager.SaveConfig();
                }
                ImGui.SameLine(0, 2);
                if (ImGui.Button("▼") && i < target.SubList.Count - 1)
                {
                    (target.SubList[i], target.SubList[i + 1]) = (target.SubList[i + 1], target.SubList[i]);
                    manager.SaveConfig();
                }
                ImGui.SameLine(0, 2);
                if (ImGui.Button("✕"))
                {
                    target.SubList.RemoveAt(i);
                    manager.SaveConfig();
                }

                ImGui.PopID();
            }

            ImGui.EndChild();

            if (ImGui.Button(Loc.Get("Shortcut.AddCommand")))
            {
                target.SubList.Add(new ShCfg { Name = Loc.Get("Shortcut.NewCommand") });
                manager.SaveConfig();
            }
            ImGui.SameLine();
            if (ImGui.Button(Loc.Get("Shortcut.AddCategory")))
            {
                target.SubList.Add(new ShCfg {
                    Name = Loc.Get("Shortcut.NewCategory"),
                    Type = ShCfg.ShortcutType.Category,
                    SubList = new List<ShCfg>()
                });
                manager.SaveConfig();
            }
            ImGui.SameLine();
            if (ImGui.Button(Loc.Get("Shortcut.AddSpacer")))
            {
                target.SubList.Add(new ShCfg { Type = ShCfg.ShortcutType.Spacer });
                manager.SaveConfig();
            }
        }

        ImGui.End();

        if (!isOpen) Close();
    }

    private static void DrawIconEditor(QoLBarManager manager, ShCfg target)
    {
        var name = target.Name;
        var hasIcon = IconManager.ParseName(ref name, out _, out _, out var iconId, out var args);

        ImGui.Text(Loc.Get("Editor.Icon"));

        // Preview
        if (hasIcon && iconId != 0)
        {
            var wrap = iconId < 0
                ? IconManager.Instance?.GetUserIconWrap(iconId)
                : IconManager.Instance?.GetGameIcon((uint)iconId, args.Contains('h') || manager.Config.Settings.UseHRIcons);

            if (IconManager.TryGetTexture(wrap, out var texture))
            {
                ImGui.Image(texture.Handle, new Vector2(32, 32));
                ImGui.SameLine();
            }
            else
            {
                ImGui.TextDisabled(Loc.Get("Editor.IconMissing"));
                ImGui.SameLine();
            }
        }

        var iconValue = hasIcon ? iconId : 0;
        if (ImGui.InputInt("##IconId", ref iconValue))
        {
            if (iconValue != 0)
                IconManager.SetIconId(ref target.Name, iconValue);
            manager.SaveConfig();
        }
        ImGui.SameLine();

        if (ImGui.Button(Loc.Get("Editor.BrowseIcons")))
        {
            IconBrowserWindow.Open(picked =>
            {
                IconManager.SetIconId(ref target.Name, picked);
                manager.SaveConfig();
            });
        }

        if (hasIcon && iconId != 0)
        {
            ImGui.SameLine();
            if (ImGui.Button(Loc.Get("Editor.ClearIcon")))
            {
                IconManager.SetIconId(ref target.Name, 0);
                target.Name = target.Name.Replace("::0", string.Empty);
                manager.SaveConfig();
            }
        }

        ImGuiEx.SetItemTooltip(Loc.Get("Editor.IconTooltip"));
    }
}
