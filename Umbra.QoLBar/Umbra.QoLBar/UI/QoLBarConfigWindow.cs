using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace Umbra.QoLBar.UI;

/// <summary>
/// The main configuration window with tabs for bars, condition sets and settings.
/// </summary>
public static class QoLBarConfigWindow
{
    public static bool   IsOpen         { get; set; }
    public static string SelectedBarId  { get; set; }

    private static int _selectedSetIndex = -1;

    public static void Draw()
    {
        if (!IsOpen) return;

        var manager = QoLBarManager.Instance;
        if (manager == null) return;

        var isOpen = IsOpen;
        ImGui.SetNextWindowSize(new Vector2(880, 660), ImGuiCond.FirstUseEver);

        if (ImGui.Begin(Loc.Get("Window.Title"), ref isOpen))
        {
            if (ImGui.BeginTabBar("##QoLBarTabs"))
            {
                if (ImGui.BeginTabItem(Loc.Get("Tab.Bars")))
                {
                    DrawBarsTab(manager);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.Get("Tab.ConditionSets")))
                {
                    DrawConditionSetsTab(manager);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.Get("Tab.Settings")))
                {
                    DrawSettingsTab(manager);
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        ImGui.End();

        IsOpen = isOpen;
        if (!IsOpen)
        {
            manager.IsConfigWindowOpen = false;
            ShortcutEditorWindow.Close();
            IconBrowserWindow.Close();
            ImportWindow.Close();
        }
    }

    #region Bars Tab

    private static void DrawBarsTab(QoLBarManager manager)
    {
        var config = manager.Config;

        if (SelectedBarId == null || !config.Bars.ContainsKey(SelectedBarId))
            SelectedBarId = config.Bars.Keys.FirstOrDefault();

        ImGui.BeginChild("##BarList", new Vector2(230, -1), true);

        ImGui.Text(Loc.Get("BarManager.Bars"));
        ImGui.Separator();

        var i = 0;
        string toDelete = null;

        foreach (var (id, bar) in config.Bars.ToList())
        {
            i++;
            var label = string.IsNullOrEmpty(bar.Name) ? $"[{i}] ({Loc.Get("Bar.Unnamed")})" : $"[{i}] {bar.Name}";

            ImGui.PushID(id);

            if (ImGui.Selectable(label, id == SelectedBarId))
            {
                SelectedBarId = id;
                ShortcutEditorWindow.Close();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{Loc.F("Bar.ShortcutCount", bar.ShortcutList.Count)}\n{id}");

            ImGui.PopID();
        }

        ImGui.Separator();

        if (ImGui.Button(Loc.Get("BarManager.New"), new Vector2(-1, 0)))
        {
            var bar = manager.CreateBar(Loc.Get("Bar.DefaultName"));
            SelectedBarId = config.Bars.FirstOrDefault(kv => kv.Value == bar).Key;
            ShortcutEditorWindow.Close();
        }

        if (SelectedBarId != null && config.Bars.ContainsKey(SelectedBarId))
        {
            var bar = config.Bars[SelectedBarId];

            if (ImGui.Button(Loc.Get("BarManager.Duplicate"), new Vector2(-1, 0)))
            {
                var copy = manager.DuplicateBar(SelectedBarId);
                if (copy != null)
                    SelectedBarId = config.Bars.FirstOrDefault(kv => kv.Value == copy).Key;
            }

            if (ImGui.Button(Loc.Get("BarManager.Delete"), new Vector2(-1, 0)))
                toDelete = SelectedBarId;

            ImGui.Separator();

            if (ImGui.Button(Loc.Get("BarManager.Export"), new Vector2(-1, 0)))
            {
                var data = manager.ExportBar(SelectedBarId);
                if (data != null)
                {
                    ImGui.SetClipboardText(data);
                    QoLBarManager.PrintEcho(Loc.Get("BarManager.ExportedToClipboard"));
                }
            }

            if (ImGui.Button(Loc.Get("BarManager.Import"), new Vector2(-1, 0)))
                ImportWindow.Open("bar");
        }

        if (toDelete != null)
        {
            if (manager.Config.Settings.ExportOnDelete)
            {
                var data = manager.ExportBar(toDelete);
                if (data != null) ImGui.SetClipboardText(data);
            }

            manager.DeleteBar(toDelete);
            if (SelectedBarId == toDelete) SelectedBarId = config.Bars.Keys.FirstOrDefault();
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##BarEditor", new Vector2(-1, -1), true);

        if (SelectedBarId == null || !config.Bars.ContainsKey(SelectedBarId))
        {
            ImGui.TextDisabled(Loc.Get("BarManager.NoBarSelected"));
            ImGui.EndChild();
            return;
        }

        DrawBarEditor(manager, config.Bars[SelectedBarId]);

        ImGui.EndChild();
    }

    private static void DrawBarEditor(QoLBarManager manager, BarCfg bar)
    {
        ImGui.Text(Loc.Get("Bar.GeneralTab"));
        ImGui.Separator();

        var name = bar.Name;
        if (ImGui.InputText(Loc.Get("Bar.Name"), ref name, 128))
        {
            bar.Name = name;
            manager.SaveConfig();
        }

        var hotkey = bar.Hotkey;
        if (KeybindManager.InputHotkey("##BarHotkey", ref hotkey))
        {
            bar.Hotkey = hotkey;
            manager.SaveConfig();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.Get("Keybind.BarHotkeyTooltip"));

        var hidden = bar.Hidden;
        if (ImGui.Checkbox(Loc.Get("Bar.Hidden"), ref hidden))
        {
            bar.Hidden = hidden;
            manager.SaveConfig();
        }
        ImGuiEx.SetItemTooltip(Loc.Get("Bar.HiddenTooltip"));

        var sets = manager.Config.ConditionSets;
        var preview = bar.ConditionSet >= 0 && bar.ConditionSet < sets.Count
            ? $"[{bar.ConditionSet + 1}] {sets[bar.ConditionSet].Name}"
            : Loc.Get("Bar.NoConditionSet");

        if (ImGui.BeginCombo(Loc.Get("Bar.ConditionSet"), preview))
        {
            if (ImGui.Selectable(Loc.Get("Bar.NoConditionSet"), bar.ConditionSet < 0))
            {
                bar.ConditionSet = -1;
                manager.SaveConfig();
            }

            for (var i = 0; i < sets.Count; i++)
            {
                if (!ImGui.Selectable($"[{i + 1}] {sets[i].Name}", bar.ConditionSet == i)) continue;
                bar.ConditionSet = i;
                manager.SaveConfig();
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Text(Loc.F("Shortcut.ListHeader", bar.ShortcutList.Count));
        ImGui.Separator();

        ImGui.BeginChild("##ShortcutList", new Vector2(-1, -70), true);

        for (var i = 0; i < bar.ShortcutList.Count; i++)
        {
            ImGui.PushID(i);
            DrawShortcutRow(manager, bar.ShortcutList, i);
            ImGui.PopID();
        }

        ImGui.EndChild();

        if (ImGui.Button(Loc.Get("Shortcut.AddCommand")))
        {
            bar.ShortcutList.Add(new ShCfg { Name = Loc.Get("Shortcut.NewCommand") });
            manager.SaveConfig();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Shortcut.AddCategory")))
        {
            bar.ShortcutList.Add(new ShCfg {
                Name = Loc.Get("Shortcut.NewCategory"),
                Type = ShCfg.ShortcutType.Category,
                SubList = new List<ShCfg>()
            });
            manager.SaveConfig();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Shortcut.AddSpacer")))
        {
            bar.ShortcutList.Add(new ShCfg { Type = ShCfg.ShortcutType.Spacer });
            manager.SaveConfig();
        }

        ImGui.Spacing();
        ImGui.TextDisabled(Loc.Get("BarManager.AddWidgetHint"));
    }

    private static void DrawShortcutRow(QoLBarManager manager, List<ShCfg> list, int index)
    {
        var sh = list[index];

        var name = sh.Name;
        var hasIcon = IconManager.ParseName(ref name, out var subName, out _, out _, out _);
        var display = sh.Type switch
        {
            ShCfg.ShortcutType.Spacer => Loc.Get("Shortcut.TypeSpacer"),
            ShCfg.ShortcutType.Category => string.IsNullOrEmpty(name) ? Loc.Get("Shortcut.TypeCategory") : name,
            _ => hasIcon && !string.IsNullOrEmpty(subName) ? subName : (string.IsNullOrEmpty(name) ? Loc.Get("Shortcut.TypeCommand") : name)
        };

        ImGui.Button($"{display}##row", new Vector2(240, 0));
        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(sh.Command))
            ImGui.SetTooltip(sh.Command);
        if (ImGui.IsItemClicked())
            ShortcutEditorWindow.Open(QoLBarConfigWindow.SelectedBarId, sh);

        ImGui.SameLine();

        var typeLabel = sh.Type switch
        {
            ShCfg.ShortcutType.Spacer => Loc.Get("Shortcut.TypeSpacer"),
            ShCfg.ShortcutType.Category => Loc.Get("Shortcut.TypeCategory"),
            _ => Loc.Get("Shortcut.TypeCommand")
        };
        ImGui.TextDisabled(typeLabel);

        if (sh.Hotkey > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({KeybindManager.GetKeyName(sh.Hotkey)})");
        }

        RightAlignButtons(index, list.Count, () =>
        {
            if (ImGui.Button("▲") && index > 0)
            {
                (list[index], list[index - 1]) = (list[index - 1], list[index]);
                manager.SaveConfig();
            }
        }, () =>
        {
            if (ImGui.Button("▼") && index < list.Count - 1)
            {
                (list[index], list[index + 1]) = (list[index + 1], list[index]);
                manager.SaveConfig();
            }
        }, () =>
        {
            if (ImGui.Button("⧉"))
            {
                list.Insert(index + 1, DeepCopy(sh));
                manager.SaveConfig();
            }
        }, () =>
        {
            if (ImGui.Button("✕"))
            {
                list.RemoveAt(index);
                manager.SaveConfig();
            }
        });
    }

    /// <summary>
    /// Lays out the given button draw actions at the right edge of the current line.
    /// </summary>
    private static void RightAlignButtons(int index, int count, params Action[] drawButtons)
    {
        var buttonWidth = ImGui.GetFrameHeight() + 6;
        var totalWidth = buttonWidth * drawButtons.Length + (drawButtons.Length - 1) * 2;
        var targetX = ImGui.GetWindowContentRegionMax().X - totalWidth;

        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(targetX, ImGui.GetCursorPosX()));

        for (var i = 0; i < drawButtons.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, 2);
            drawButtons[i]();
        }
    }

    private static ShCfg DeepCopy(ShCfg sh)
    {
        var json = JsonConvert.SerializeObject(sh);
        return JsonConvert.DeserializeObject<ShCfg>(json);
    }

    #endregion

    #region Condition Sets Tab

    private static void DrawConditionSetsTab(QoLBarManager manager)
    {
        var config = manager.Config;

        if (_selectedSetIndex >= config.ConditionSets.Count) _selectedSetIndex = config.ConditionSets.Count - 1;

        ImGui.BeginChild("##SetList", new Vector2(230, -1), true);

        ImGui.Text(Loc.Get("ConditionSet.Sets"));
        ImGui.Separator();

        for (var i = 0; i < config.ConditionSets.Count; i++)
        {
            var set = config.ConditionSets[i];
            var label = string.IsNullOrEmpty(set.Name) ? $"[{i + 1}] ({Loc.Get("Bar.Unnamed")})" : $"[{i + 1}] {set.Name}";

            ImGui.PushID(i);
            if (ImGui.Selectable(label, i == _selectedSetIndex))
                _selectedSetIndex = i;
            ImGui.PopID();
        }

        ImGui.Separator();

        if (ImGui.Button(Loc.Get("ConditionSet.NewSet"), new Vector2(-1, 0)))
        {
            config.ConditionSets.Add(new CndSetCfg { Name = Loc.Get("ConditionSet.DefaultName") });
            _selectedSetIndex = config.ConditionSets.Count - 1;
            manager.SaveConfig();
        }

        if (_selectedSetIndex >= 0 && _selectedSetIndex < config.ConditionSets.Count)
        {
            if (ImGui.Button(Loc.Get("ConditionSet.Duplicate"), new Vector2(-1, 0)))
            {
                config.ConditionSets.Insert(_selectedSetIndex + 1, DeepCopy(config.ConditionSets[_selectedSetIndex]));
                _selectedSetIndex++;
                manager.SaveConfig();
            }

            if (ImGui.Button(Loc.Get("ConditionSet.Delete"), new Vector2(-1, 0)))
            {
                ConditionManager.RemoveConditionSet(_selectedSetIndex);
                if (_selectedSetIndex >= config.ConditionSets.Count) _selectedSetIndex = config.ConditionSets.Count - 1;
            }

            ImGui.Separator();

            if (ImGui.Button(Loc.Get("ConditionSet.Export"), new Vector2(-1, 0)))
            {
                var data = manager.ExportConditionSet(_selectedSetIndex);
                if (data != null)
                {
                    ImGui.SetClipboardText(data);
                    QoLBarManager.PrintEcho(Loc.Get("BarManager.ExportedToClipboard"));
                }
            }

            if (ImGui.Button(Loc.Get("ConditionSet.Import"), new Vector2(-1, 0)))
                ImportWindow.Open("set");
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##SetEditor", new Vector2(-1, -1), true);

        if (_selectedSetIndex < 0 || _selectedSetIndex >= config.ConditionSets.Count)
        {
            ImGui.TextDisabled(Loc.Get("ConditionSet.NoSetSelected"));
            ImGui.EndChild();
            return;
        }

        DrawConditionSetEditor(manager, config.ConditionSets[_selectedSetIndex]);

        ImGui.EndChild();
    }

    private static void DrawConditionSetEditor(QoLBarManager manager, CndSetCfg set)
    {
        var name = set.Name;
        if (ImGui.InputText(Loc.Get("ConditionSet.Name"), ref name, 128))
        {
            set.Name = name;
            manager.SaveConfig();
        }

        ImGui.Separator();

        for (var i = 0; i < set.Conditions.Count; i++)
        {
            ImGui.PushID(i);
            DrawConditionRow(manager, set, i);
            ImGui.PopID();
        }

        ImGui.Spacing();

        if (ImGui.BeginCombo("##AddCondition", Loc.Get("ConditionSet.AddCondition")))
        {
            foreach (var (category, conditions) in ConditionManager.ConditionCategories)
            {
                if (conditions.Count == 0) continue;

                ImGui.Separator();
                ImGui.TextDisabled(category.CategoryName);

                foreach (var condition in conditions)
                {
                    if (!ImGui.Selectable("    " + condition.ConditionName)) continue;

                    var cnd = new CndCfg { ID = condition.ID };

                    if (condition is IArgCondition argCondition)
                        cnd.Arg = argCondition.GetDefaultArg(cnd);

                    if (condition is IOnImportCondition importCondition)
                        importCondition.OnImport(cnd);

                    set.Conditions.Add(cnd);
                    manager.SaveConfig();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();

        if (ImGui.BeginCombo("##AddPreset", Loc.Get("ConditionSet.AddPreset")))
        {
            foreach (var preset in ConditionManager.Presets)
            {
                if (!ImGui.Selectable(preset.Name)) continue;

                var generated = preset.Generate();
                if (generated == null) continue;

                generated.Name = preset.Name;
                manager.Config.ConditionSets.Add(generated);
                manager.SaveConfig();
            }

            ImGui.EndCombo();
        }
    }

    private static void DrawConditionRow(QoLBarManager manager, CndSetCfg set, int index)
    {
        var cnd = set.Conditions[index];
        var condition = ConditionManager.GetCondition(cnd.ID);

        var negate = cnd.Negate;
        if (ImGui.Checkbox("##negate", ref negate))
        {
            cnd.Negate = negate;
            manager.SaveConfig();
        }
        ImGuiEx.SetItemTooltip(Loc.Get("ConditionSet.NegateTooltip"));

        ImGui.SameLine();

        if (index == 0)
        {
            ImGui.TextDisabled(Loc.Get("ConditionSet.Where"));
        }
        else
        {
            var op = (int)cnd.Operator;
            var opNames = Enum.GetNames(typeof(ConditionManager.BinaryOperator));
            if (ImGui.Combo("##operator", ref op, opNames, opNames.Length))
            {
                cnd.Operator = (ConditionManager.BinaryOperator)op;
                manager.SaveConfig();
            }
        }

        ImGui.SameLine();

        var preview = condition?.ConditionName ?? cnd.ID;
        ImGui.SetNextItemWidth(180);
        if (ImGui.BeginCombo("##condition", preview))
        {
            foreach (var (category, conditions) in ConditionManager.ConditionCategories)
            {
                if (conditions.Count == 0) continue;

                ImGui.Separator();
                ImGui.TextDisabled(category.CategoryName);

                foreach (var c in conditions)
                {
                    if (!ImGui.Selectable("    " + c.ConditionName, c.ID == cnd.ID)) continue;

                    cnd.ID = c.ID;

                    if (c is IArgCondition argCondition)
                        cnd.Arg = argCondition.GetDefaultArg(cnd);
                    else
                        cnd.Arg = 0;

                    manager.SaveConfig();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();

        if (condition is IDrawableCondition drawable)
        {
            ImGui.BeginGroup();
            drawable.Draw(cnd);
            ImGui.EndGroup();
        }

        RightAlignButtons(index, set.Conditions.Count, () =>
        {
            if (ImGui.Button("▲") && index > 0)
                ConditionManager.ShiftCondition(set, cnd, false);
        }, () =>
        {
            if (ImGui.Button("▼") && index < set.Conditions.Count - 1)
                ConditionManager.ShiftCondition(set, cnd, true);
        }, () =>
        {
            if (ImGui.Button("✕"))
            {
                set.Conditions.RemoveAt(index);
                manager.SaveConfig();
            }
        });
    }

    private static CndSetCfg DeepCopy(CndSetCfg set)
    {
        var json = JsonConvert.SerializeObject(set);
        var copy = JsonConvert.DeserializeObject<CndSetCfg>(json);
        copy.Name = set.Name + " " + Loc.Get("Bar.CopySuffix");
        return copy;
    }

    #endregion

    #region Settings Tab

    private static void DrawSettingsTab(QoLBarManager manager)
    {
        var settings = manager.Config.Settings;

        var language = settings.Language;
        if (ImGui.BeginCombo(Loc.Get("Settings.Language"), Loc.Get($"Lang.{language}", language)))
        {
            foreach (var (code, _) in Loc.Languages)
            {
                if (!ImGui.Selectable(Loc.Get($"Lang.{code}", code), language == code)) continue;
                settings.Language = code;
                Loc.Load(code);
                manager.SaveConfig();
            }
            ImGui.EndCombo();
        }

        var useHRIcons = settings.UseHRIcons;
        if (ImGui.Checkbox(Loc.Get("Settings.UseHRIcons"), ref useHRIcons))
        {
            settings.UseHRIcons = useHRIcons;
            manager.SaveConfig();
        }
        ImGuiEx.SetItemTooltip(Loc.Get("Settings.UseHRIconsTooltip"));

        var noCache = settings.NoConditionCache;
        if (ImGui.Checkbox(Loc.Get("Settings.NoConditionCache"), ref noCache))
        {
            settings.NoConditionCache = noCache;
            manager.SaveConfig();
        }
        ImGuiEx.SetItemTooltip(Loc.Get("Settings.NoConditionCacheTooltip"));

        var exportOnDelete = settings.ExportOnDelete;
        if (ImGui.Checkbox(Loc.Get("Settings.ExportOnDelete"), ref exportOnDelete))
        {
            settings.ExportOnDelete = exportOnDelete;
            manager.SaveConfig();
        }
        ImGuiEx.SetItemTooltip(Loc.Get("Settings.ExportOnDeleteTooltip"));

        var showHint = settings.ShowWidgetHint;
        if (ImGui.Checkbox(Loc.Get("Settings.ShowWidgetHint"), ref showHint))
        {
            settings.ShowWidgetHint = showHint;
            manager.SaveConfig();
        }
        ImGuiEx.SetItemTooltip(Loc.Get("Settings.ShowWidgetHintTooltip"));

        ImGui.Separator();
        ImGui.Text(Loc.Get("Settings.About"));
        ImGui.Separator();
        ImGui.TextWrapped(Loc.Get("Settings.AboutBody"));

        ImGui.Spacing();
        ImGui.TextDisabled(Loc.F("Settings.ConfigPath", System.IO.Path.Combine(Umbra.Common.Framework.DalamudPlugin.ConfigDirectory.FullName, "QoLBar", "config.json")));
    }

    #endregion
}
