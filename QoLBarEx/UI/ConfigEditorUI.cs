using System;
using System.Numerics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using static QoLBar.BarCfg;
using static QoLBar.ShCfg;

namespace QoLBar;

// I hate this file with a passion
public static class ConfigEditorUI
{
    private static int _inputPos = 0;
    private static unsafe int GetCursorPosCallback(ImGuiInputTextCallbackDataPtr data)
    {
        _inputPos = data.CursorPos;
        return 0;
    }

    private static void DrawInsertPrivateCharPopup(ref string input)
    {
        if (ImGui.BeginPopup($"Private Use Popup##{ImGui.GetCursorPos()}"))
        {
            var temp = input;
            static void InsertString(ref string str, string ins)
            {
                var bytes = Encoding.UTF8.GetBytes(str).ToList();
                _inputPos = Math.Min(_inputPos, bytes.Count);
                var newBytes = Encoding.UTF8.GetBytes(ins);
                for (int i = 0; i < newBytes.Length; i++)
                    bytes.Insert(_inputPos++, newBytes[i]);
                str = Encoding.UTF8.GetString(bytes.ToArray());
            }

            var bI = 0;
            void DrawButton(int i)
            {
                if (bI % 15 != 0)
                    ImGui.SameLine();

                var str = $"{(char)i}";
                ImGui.SetWindowFontScale(1.5f);
                if (ImGui.Button(str, new Vector2(36 * ImGuiHelpers.GlobalScale)))
                {
                    InsertString(ref temp, str);
                    QoLBar.Config.Save();
                }
                ImGui.SetWindowFontScale(1);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.SetWindowFontScale(4);
                    ImGui.TextUnformatted(str);
                    ImGui.SetWindowFontScale(1);
                    ImGui.TextUnformatted($"{(Dalamud.Game.Text.SeIconChar)i}");
                    ImGui.EndTooltip();
                }

                bI++;
            }


            for (var i = 0xE020; i <= 0xE02B; i++)
                DrawButton(i);
            for (var i = 0xE031; i <= 0xE035; i++)
                DrawButton(i);
            for (var i = 0xE038; i <= 0xE044; i++)
                DrawButton(i);
            for (var i = 0xE048; i <= 0xE04E; i++)
                DrawButton(i);
            for (var i = 0xE050; i <= 0xE08A; i++)
                DrawButton(i);
            for (var i = 0xE08F; i <= 0xE0C6; i++)
                DrawButton(i);
            for (var i = 0xE0D0; i <= 0xE0DB; i++)
                DrawButton(i);

            input = temp;

            ImGui.EndPopup();
        }
    }

    private static void AddRightClickPrivateUsePopup(ref string input)
    {
        if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"Private Use Popup##{ImGui.GetCursorPos()}");

        DrawInsertPrivateCharPopup(ref input);
    }

    public static void AutoPasteIcon(ShCfg sh)
    {
        if (!IconBrowserUI.iconBrowserOpen || !IconBrowserUI.doPasteIcon) return;

        var split = sh.Name.Split(new[] { "##" }, 2, StringSplitOptions.None);
        var split2 = split[0].Split(new[] { "::" }, 2, StringSplitOptions.None);
        sh.Name = $"{split2[0]}::{IconBrowserUI.pasteIcon}" + (split.Length > 1 ? $"##{split[1]}" : "");
        QoLBar.Config.Save();
        IconBrowserUI.doPasteIcon = false;
    }

    public static void EditShortcutConfigBase(ShCfg sh, bool editing, bool hasIcon)
    {
        EditShortcutName(sh, editing);
        ImGuiEx.SetItemTooltip(Localization.Get("Editor.NameTooltip")
                               + (hasIcon ? Localization.Get("Editor.NameTooltipIcons") : string.Empty));

        var _t = (int)sh.Type;
        ImGui.TextUnformatted(Localization.Get("Editor.Type"));
        ImGui.RadioButton(Localization.Get("Editor.TypeCommand"), ref _t, 0);
        ImGui.SameLine(ImGui.GetWindowWidth() / 3);
        ImGui.RadioButton(Localization.Get("Editor.TypeCategory"), ref _t, 1);
        ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
        ImGui.RadioButton(Localization.Get("Editor.TypeSpacer"), ref _t, 2);
        if (_t != (int)sh.Type)
        {
            sh.Type = (ShortcutType)_t;
            if (sh.Type == ShortcutType.Category)
                sh.SubList ??= new List<ShCfg>();

            if (editing)
                QoLBar.Config.Save();
        }

        if (sh.Type != ShortcutType.Spacer && (sh.Type != ShortcutType.Category || sh.Mode == ShortcutMode.Default))
        {
            var height = ImGui.GetFontSize() * Math.Min(sh.Command.Split('\n').Length + 1, 7) + ImGui.GetStyle().FramePadding.Y * 2; // ImGui issue #238: can't disable multiline scrollbar and it appears a whole line earlier than it should, so thats cool I guess

            unsafe
            {
                if (ImGui.InputTextMultiline("Command##Input", ref sh.Command, 65535, new Vector2(0, height), ImGuiInputTextFlags.CallbackAlways, GetCursorPosCallback) && editing)
                    QoLBar.Config.Save();
            }
            AddRightClickPrivateUsePopup(ref sh.Command);
            ImGuiEx.SetItemTooltip(Localization.Get("Editor.CommandTooltip"));
        }
    }

    public static unsafe bool EditShortcutName(ShCfg sh, bool editing)
    {
        var ret = ImGui.InputText(Localization.Get("Editor.Name"), ref sh.Name, 256, ImGuiInputTextFlags.CallbackAlways, GetCursorPosCallback);
        AddRightClickPrivateUsePopup(ref sh.Name);

        if (ret && editing)
            QoLBar.Config.Save();

        return ret;
    }

    public static bool EditShortcutMode(ShortcutUI sh)
    {
        var _m = (int)sh.Config.Mode;
        ImGui.TextUnformatted(Localization.Get("Editor.Mode"));
        ImGuiEx.SetItemTooltip(Localization.Get("Editor.ModeTooltip"));

        ImGui.RadioButton(Localization.Get("Editor.ModeDefault"), ref _m, 0);
        ImGuiEx.SetItemTooltip(Localization.Get("Editor.ModeDefaultTooltip"));

        ImGui.SameLine(ImGui.GetWindowWidth() / 3);
        ImGui.RadioButton(Localization.Get("Editor.ModeIncremental"), ref _m, 1);
        ImGuiEx.SetItemTooltip(Localization.Get("Editor.ModeIncrementalTooltip"));

        ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
        ImGui.RadioButton(Localization.Get("Editor.ModeRandom"), ref _m, 2);
        ImGuiEx.SetItemTooltip(Localization.Get("Editor.ModeRandomTooltip"));

        if (_m != (int)sh.Config.Mode)
        {
            sh.Config.Mode = (ShortcutMode)_m;

            if (sh.Config.Mode == ShortcutMode.Random)
            {
                var c = Math.Max(1, (sh.Config.Type == ShortcutType.Category) ? sh.children.Count : sh.Config.Command.Split('\n').Length);
                sh.Config._i = (int)(QoLBar.FrameCount % c);
            }
            else
            {
                sh.Config._i = 0;
            }

            QoLBar.Config.Save();

            return true;
        }

        return false;
    }

    public static bool EditShortcutColor(ShortcutUI sh)
    {
        var color = ImGui.ColorConvertU32ToFloat4(sh.Config.Color);
        color.W += sh.Config.ColorAnimation / 255f; // Temporary
        if (ImGui.ColorEdit4(Localization.Get("Editor.Color"), ref color, ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.AlphaPreviewHalf))
        {
            sh.Config.Color = ImGui.ColorConvertFloat4ToU32(color);
            sh.Config.ColorAnimation = Math.Max((int)Math.Round(color.W * 255) - 255, 0);
            QoLBar.Config.Save();
            return true;
        }
        else
        {
            return false;
        }
    }

    public static void EditShortcutCategoryOptions(ShortcutUI sh)
    {
        if (ImGui.SliderInt(Localization.Get("Category.ButtonWidth"), ref sh.Config.CategoryWidth, 0, 200))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip(Localization.Get("Category.ButtonWidthTooltip"));

        if (ImGui.SliderInt(Localization.Get("Category.Columns"), ref sh.Config.CategoryColumns, 0, 12))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip(Localization.Get("Category.ColumnsTooltip"));

        if (ImGui.DragFloat(Localization.Get("Category.Scale"), ref sh.Config.CategoryScale, 0.002f, 0.7f, 2f, "%.2f"))
            QoLBar.Config.Save();

        if (ImGui.DragFloat(Localization.Get("Category.FontScale"), ref sh.Config.CategoryFontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
            QoLBar.Config.Save();

        var spacing = new Vector2(sh.Config.CategorySpacing[0], sh.Config.CategorySpacing[1]);
        if (ImGui.DragFloat2(Localization.Get("Category.Spacing"), ref spacing, 0.12f, 0, 32, "%.f"))
        {
            sh.Config.CategorySpacing[0] = (int)spacing.X;
            sh.Config.CategorySpacing[1] = (int)spacing.Y;
            QoLBar.Config.Save();
        }

        if (ImGui.Checkbox(Localization.Get("Category.OpenOnHover"), ref sh.Config.CategoryOnHover))
            QoLBar.Config.Save();
        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
        if (ImGui.Checkbox(Localization.Get("Category.CloseWhenNotHovered"), ref sh.Config.CategoryHoverClose))
            QoLBar.Config.Save();

        if (ImGui.Checkbox(Localization.Get("Category.StayOpenOnSelection"), ref sh.Config.CategoryStaysOpen))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip(Localization.Get("Category.StayOpenTooltip"));
        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
        if (ImGui.Checkbox(Localization.Get("Category.NoBackground"), ref sh.Config.CategoryNoBackground))
            QoLBar.Config.Save();
    }

    public static void EditShortcutIconOptions(ShortcutUI sh)
    {
        if (ImGui.DragFloat(Localization.Get("Icon.Zoom"), ref sh.Config.IconZoom, 0.005f, 1.0f, 5.0f, "%.2f"))
            QoLBar.Config.Save();

        var offset = new Vector2(sh.Config.IconOffset[0], sh.Config.IconOffset[1]);
        if (ImGui.DragFloat2(Localization.Get("Icon.Offset"), ref offset, 0.0005f, -0.5f, 0.5f, "%.3f"))
        {
            sh.Config.IconOffset[0] = offset.X;
            sh.Config.IconOffset[1] = offset.Y;
            QoLBar.Config.Save();
        }

        var r = (float)(sh.Config.IconRotation * 180 / Math.PI) % 360;
        if (ImGui.DragFloat(Localization.Get("Icon.Rotation"), ref r, 0.2f, -360, 360, "%.f"))
        {
            if (r < 0)
                r += 360;
            sh.Config.IconRotation = (float)(r / 180 * Math.PI);
            QoLBar.Config.Save();
        }

        static string formatName(Lumina.Excel.Sheets.Action a) => a.RowId switch
        {
            0 => Localization.Get("Common.None"),
            847 => Localization.Get("Icon.Item847"),
            _ => $"[{a.RowId}] {a.Name}"
        };
        if (ImGuiEx.ExcelSheetCombo<Lumina.Excel.Sheets.Action>(Localization.Get("Icon.CooldownActionID"), out var action, s => s.GetRowOrDefault(sh.Config.CooldownAction) is { } a ? formatName(a) : sh.Config.CooldownAction.ToString(),
            ImGuiComboFlags.None, (a, s) => (a.RowId == 0 || a is { CooldownGroup: > 0, ClassJobCategory.RowId: > 0 }) && formatName(a).Contains(s, StringComparison.CurrentCultureIgnoreCase),
            a => ImGui.Selectable(formatName(a), sh.Config.CooldownAction == a.RowId)))
        {
            sh.Config.CooldownAction = action.Value.RowId;

            if (sh.Config.CooldownAction == 0)
                sh.Config.CooldownStyle = 0;

            QoLBar.Config.Save();
        }

        if (sh.Config.CooldownAction > 0)
        {
            var save = ImGui.CheckboxFlags("##CooldownNumber", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.Number);
            ImGuiEx.SetItemTooltip(Localization.Get("Icon.CooldownNumber"));
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##CooldownDisable", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.Disable);
            ImGuiEx.SetItemTooltip(Localization.Get("Icon.CooldownDisable"));
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##CooldownDefault", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.Cooldown);
            ImGuiEx.SetItemTooltip(Localization.Get("Icon.CooldownDefault"));
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##CooldownGCD", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.GCDCooldown);
            ImGuiEx.SetItemTooltip(Localization.Get("Icon.CooldownGCD"));
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("Cooldown Style Flags##CooldownCharge", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.ChargeCooldown);
            ImGuiEx.SetItemTooltip(Localization.Get("Icon.CooldownCharge"));
            if (save)
                QoLBar.Config.Save();
        }
    }

    public static void EditBarGeneralOptions(BarUI bar)
    {
        if (ImGui.InputText(Localization.Get("Bar.Name"), ref bar.Config.Name, 256))
            QoLBar.Config.Save();

        var _dock = (int)bar.Config.DockSide;
        var dockItems = string.Join("\0",
            Localization.Get("Bar.DockTop"),
            Localization.Get("Bar.DockRight"),
            Localization.Get("Bar.DockBottom"),
            Localization.Get("Bar.DockLeft"),
            (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0 ? Localization.Get("Bar.DockUndocked") : string.Empty);
        if (ImGui.Combo(Localization.Get("Bar.Side"), ref _dock, dockItems))
        {
            bar.Config.DockSide = (BarDock)_dock;
            if (bar.Config.DockSide == BarDock.Undocked && bar.Config.Visibility == BarVisibility.Slide)
                bar.Config.Visibility = BarVisibility.Always;
            bar.Config.Position[0] = 0;
            bar.Config.Position[1] = 0;
            bar.Config.LockedPosition = false;
            QoLBar.Config.Save();
            bar.SetupPivot();
        }

        if (bar.IsDocked)
        {
            var topbottom = bar.Config.DockSide == BarDock.Top || bar.Config.DockSide == BarDock.Bottom;
            var _align = (int)bar.Config.Alignment;
            ImGui.Text(Localization.Get("Bar.Alignment"));
            ImGui.RadioButton(Localization.Get(topbottom ? "Bar.AlignLeft" : "Bar.AlignTop"), ref _align, 0);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
            ImGui.RadioButton(Localization.Get("Bar.AlignCenter"), ref _align, 1);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
            ImGui.RadioButton(Localization.Get(topbottom ? "Bar.AlignRight" : "Bar.AlignBottom"), ref _align, 2);
            if (_align != (int)bar.Config.Alignment)
            {
                bar.Config.Alignment = (BarAlign)_align;
                QoLBar.Config.Save();
                bar.SetupPivot();
            }

            var _visibility = (int)bar.Config.Visibility;
            ImGui.Text(Localization.Get("Bar.Animation"));
            ImGui.RadioButton(Localization.Get("Bar.AnimSlide"), ref _visibility, 0);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
            ImGui.RadioButton(Localization.Get("Bar.AnimImmediate"), ref _visibility, 1);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
            ImGui.RadioButton(Localization.Get("Bar.AnimAlways"), ref _visibility, 2);
            if (_visibility != (int)bar.Config.Visibility)
            {
                bar.Config.Visibility = (BarVisibility)_visibility;
                QoLBar.Config.Save();
            }

            if ((bar.Config.Visibility != BarVisibility.Always) && ImGui.DragFloat(Localization.Get("Bar.RevealAreaScale"), ref bar.Config.RevealAreaScale, 0.01f, 0.0f, 1.0f, "%.2f"))
                QoLBar.Config.Save();
        }
        else
        {
            var _visibility = (int)bar.Config.Visibility;
            ImGui.Text(Localization.Get("Bar.Animation"));
            ImGui.RadioButton(Localization.Get("Bar.AnimImmediate"), ref _visibility, 1);
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            ImGui.RadioButton(Localization.Get("Bar.AnimAlways"), ref _visibility, 2);
            if (_visibility != (int)bar.Config.Visibility)
            {
                bar.Config.Visibility = (BarVisibility)_visibility;
                QoLBar.Config.Save();
            }
        }

        Keybind.KeybindInput(bar.Config);

        if (ImGui.Checkbox(Localization.Get("Bar.EditMode"), ref bar.Config.Editing))
        {
            if (!bar.Config.Editing)
                Game.ExecuteCommand(Localization.Get("Bar.EditModeEcho"));
            QoLBar.Config.Save();
        }
        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
        if (ImGui.Checkbox(Localization.Get("Bar.ClickThrough"), ref bar.Config.ClickThrough))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip(Localization.Get("Bar.ClickThroughTooltip"));

        if (ImGui.Checkbox(Localization.Get("Bar.LockPosition"), ref bar.Config.LockedPosition))
            QoLBar.Config.Save();
        if (bar.IsDocked && bar.Config.Visibility != BarVisibility.Always)
        {
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            if (ImGui.Checkbox(Localization.Get("Bar.Hint"), ref bar.Config.Hint))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip(Localization.Get("Bar.HintTooltip"));
        }

        if (!bar.Config.LockedPosition)
        {
            var pos = bar.VectorPosition;
            var area = bar.UsableArea;
            var max = (area.X > area.Y) ? area.X : area.Y;
            if (ImGui.DragFloat2(Localization.Get(bar.IsDocked ? "Bar.Offset" : "Bar.Position"), ref pos, 1, -max, max, "%.f"))
            {
                bar.Config.Position[0] = Math.Min(pos.X / area.X, 1);
                bar.Config.Position[1] = Math.Min(pos.Y / area.Y, 1);
                QoLBar.Config.Save();
                if (bar.IsDocked)
                    bar.SetupPivot();
                else
                    bar._setPos = true;
            }
        }
    }

    public static void EditBarStyleOptions(BarUI bar)
    {
        if (ImGui.SliderInt(Localization.Get("Category.ButtonWidth"), ref bar.Config.ButtonWidth, 0, 200))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip(Localization.Get("Category.ButtonWidthTooltip"));

        if (ImGui.SliderInt(Localization.Get("Category.Columns"), ref bar.Config.Columns, 0, 12))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip(Localization.Get("Category.ColumnsTooltip"));

        if (ImGui.DragFloat(Localization.Get("Category.Scale"), ref bar.Config.Scale, 0.002f, 0.7f, 2.0f, "%.2f"))
            QoLBar.Config.Save();

        if (ImGui.DragFloat(Localization.Get("Category.FontScale"), ref bar.Config.FontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
            QoLBar.Config.Save();

        var spacing = new Vector2(bar.Config.Spacing[0], bar.Config.Spacing[1]);
        if (ImGui.DragFloat2(Localization.Get("Category.Spacing"), ref spacing, 0.12f, 0, 32, "%.f"))
        {
            bar.Config.Spacing[0] = (int)spacing.X;
            bar.Config.Spacing[1] = (int)spacing.Y;
            QoLBar.Config.Save();
        }

        if (ImGui.Checkbox(Localization.Get("Category.NoBackground"), ref bar.Config.NoBackground))
            QoLBar.Config.Save();
    }

    public static void DisplayRightClickDeleteMessage(string text = null) =>
        DalamudApi.ShowNotification($"\t\t\t{text ?? Localization.Get("Common.RightClickToDelete")}\t\t\t\n\n", NotificationType.Info);
}