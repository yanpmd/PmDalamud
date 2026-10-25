using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Umbra.Common;
using Umbra.Game;

namespace Umbra.QoLBar.Conditions;

public static class MiscConditionHelpers
{
    public const string TimespanRegex = @"^([0-9Xx]{1,2}:[0-9Xx]{2})\s*-\s*([0-9Xx]{1,2}:[0-9Xx]{2})$";

    private static (double, double, double, double) ParseTime(string str) => str.Length switch
    {
        4 => (0, char.GetNumericValue(str[0]), char.GetNumericValue(str[2]), char.GetNumericValue(str[3])),
        5 => (char.GetNumericValue(str[0]), char.GetNumericValue(str[1]), char.GetNumericValue(str[3]), char.GetNumericValue(str[4])),
        _ => (0, 0, 0, 0)
    };

    public static bool IsTimeBetween(string tStr, string minStr, string maxStr)
    {
        var t = ParseTime(tStr);
        var min = ParseTime(minStr);
        var max = ParseTime(maxStr);

        var minTime = 0.0;
        var maxTime = 0.0;
        var curTime = 0.0;
        if (min.Item1 >= 0 && max.Item1 >= 0)
        {
            minTime += min.Item1 * 1000;
            maxTime += max.Item1 * 1000;
            curTime += t.Item1 * 1000;
        }
        if (min.Item2 >= 0 && max.Item2 >= 0)
        {
            minTime += min.Item2 * 100;
            maxTime += max.Item2 * 100;
            curTime += t.Item2 * 100;
        }
        if (min.Item3 >= 0 && max.Item3 >= 0)
        {
            minTime += min.Item3 * 10;
            maxTime += max.Item3 * 10;
            curTime += t.Item3 * 10;
        }
        if (min.Item4 >= 0 && max.Item4 >= 0)
        {
            minTime += min.Item4;
            maxTime += max.Item4;
            curTime += t.Item4;
        }
        return (minTime < maxTime) ? (minTime <= curTime && curTime < maxTime) : (minTime <= curTime || curTime < maxTime);
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class MiscConditionAttribute : Attribute, IConditionCategory
{
    public int DisplayPriority => 100;
}

[MiscCondition]
public class LoggedInCondition : ICondition
{
    public string ID => "l";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => Framework.Service<IClientState>().IsLoggedIn;
}

[MiscCondition]
public class CharacterCondition : ICondition, IDrawableCondition, IArgCondition, IOnImportCondition
{
    public string ID => "c";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => (ulong)arg == Framework.LocalCharacterId;
    public string GetTooltip(CndCfg cndCfg) => Loc.F("CharacterCondition.Tooltip", cndCfg.Arg);
    public string GetSelectableTooltip(CndCfg cndCfg) => Loc.Get("CharacterCondition.SelectableTooltip");
    public void Draw(CndCfg cndCfg)
    {
        if (cndCfg.Arg != 0)
        {
            if (ImGui.Button(Loc.Get("CharacterCondition.ClearData")))
                cndCfg.Arg = 0;
        }
        else
        {
            if (ImGui.Button(Loc.Get("CharacterCondition.AssignData")))
                cndCfg.Arg = GetDefaultArg(cndCfg);
        }

        ImGuiEx.SetItemTooltip(Loc.Get("CharacterCondition.DataTooltip"));
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => Framework.LocalCharacterId;
    public void OnImport(CndCfg cndCfg)
    {
        if (cndCfg.Arg == 0)
            cndCfg.Arg = GetDefaultArg(cndCfg);
    }
}

[MiscCondition]
public class TargetCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "t";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg)
    {
        var targetManager = Framework.Service<ITargetManager>();
        return (int)arg switch
        {
            0 => targetManager.Target != null,
            1 => targetManager.FocusTarget != null,
            2 => targetManager.SoftTarget != null,
            _ => false
        };
    }
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        var _ = (int)cndCfg.Arg;
        if (ImGui.Combo("##TargetType", ref _, Loc.Get("TargetCondition.Target") + "\0" + Loc.Get("TargetCondition.FocusTarget") + "\0" + Loc.Get("TargetCondition.SoftTarget") + "\0"))
            cndCfg.Arg = _;
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => 0;
}

[MiscCondition]
public class WeaponDrawnCondition : ICondition
{
    public string ID => "wd";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg)
    {
        var player = Framework.Service<IPlayer>();
        return player.IsWeaponDrawn;
    }
}

[MiscCondition]
public class EorzeaTimespanCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "et";
    public int DisplayPriority => 0;
    private static bool CheckEorzeaTimeCondition(string arg)
    {
        var reg = Regex.Match(arg, MiscConditionHelpers.TimespanRegex);
        return reg.Success && MiscConditionHelpers.IsTimeBetween(GameActions.EorzeaTime.ToString("HH:mm"), reg.Groups[1].Value, reg.Groups[2].Value);
    }
    public bool Check(dynamic arg) => arg is string range && CheckEorzeaTimeCondition(range);
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg) => DrawTimespanInput(cndCfg);
    public dynamic GetDefaultArg(CndCfg cndCfg) => cndCfg.Arg is string ? cndCfg.Arg : string.Empty;

    private void DrawTimespanInput(CndCfg cndCfg)
    {
        string timespan = cndCfg.Arg is string ? cndCfg.Arg : string.Empty;
        if (ImGui.InputText("##Timespan", ref timespan, 16))
        {
            cndCfg.Arg = timespan;
            QoLBarManager.Instance.SaveConfig();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.F("Timespan.Tooltip", Loc.Get("Timespan.Format")));
    }
}

[MiscCondition]
public class LocalTimespanCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "lt";
    public int DisplayPriority => 0;
    private static bool CheckLocalTimeCondition(string arg)
    {
        var reg = Regex.Match(arg, MiscConditionHelpers.TimespanRegex);
        return reg.Success && MiscConditionHelpers.IsTimeBetween(DateTime.Now.ToString("HH:mm"), reg.Groups[1].Value, reg.Groups[2].Value);
    }
    public bool Check(dynamic arg) => arg is string range && CheckLocalTimeCondition(range);
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        string timespan = cndCfg.Arg is string ? cndCfg.Arg : string.Empty;
        if (ImGui.InputText("##Timespan", ref timespan, 16))
        {
            cndCfg.Arg = timespan;
            QoLBarManager.Instance.SaveConfig();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.F("Timespan.Tooltip", Loc.Get("Timespan.Format")));
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => cndCfg.Arg is string ? cndCfg.Arg : string.Empty;
}

[MiscCondition]
public class HUDLayoutCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "hl";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => (byte)arg == GameActions.CurrentHUDLayout;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        var _ = (int)cndCfg.Arg + 1;
        if (ImGui.SliderInt("##HUDLayout", ref _, 1, 4))
            cndCfg.Arg = _ - 1;
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => (int)GameActions.CurrentHUDLayout;
}

[MiscCondition]
public class KeyHeldCondition : ICondition, IDrawableCondition
{
    public string ID => "k";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => KeybindManager.IsHotkeyHeld((int)arg, false);
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        var _ = (int)cndCfg.Arg;
        if (KeybindManager.InputHotkey("##KeyHeldCondition", ref _))
            cndCfg.Arg = _;
        ImGuiEx.SetItemTooltip(Loc.Get("Keybind.ClearTooltip"));
    }
}

[MiscCondition]
public class PartyCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "pt";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => Framework.Service<IPartyList>().Length >= (int)arg;
    public string GetTooltip(CndCfg cndCfg) => Loc.Get("PartyCondition.Tooltip");
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        var _ = (int)cndCfg.Arg;
        if (ImGui.SliderInt("##MemberCount", ref _, 2, 8))
            cndCfg.Arg = _;
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => 2;
}

[MiscCondition]
public class PetCondition : ICondition
{
    public string ID => "pe";
    public int DisplayPriority => 0;

    // ObjectKind.BattleNpc = 2, BattleNpcSubKind.Pet = 2
    public bool Check(dynamic arg) => CompanionHelpers.HasOwnedCompanion(2);
}

[MiscCondition]
public class ChocoboCondition : ICondition
{
    public string ID => "ce";
    public int DisplayPriority => 0;

    // ObjectKind.BattleNpc = 2, BattleNpcSubKind.Buddy = 3
    public bool Check(dynamic arg) => CompanionHelpers.HasOwnedCompanion(3);

}

/// <summary>
/// Helpers shared by the companion related conditions.
/// </summary>
public static class CompanionHelpers
{
    /// <summary>
    /// Returns true if the local player owns an existing actor of the given
    /// battle NPC subkind (pet or buddy chocobo).
    /// </summary>
    public static bool HasOwnedCompanion(byte subKind)
    {
        var objectTable = Framework.Service<IObjectTable>();
        if (objectTable.LocalPlayer is not { } player) return false;

        return objectTable.Any(o => o.OwnerId == player.GameObjectId && (byte)o.ObjectKind == 2 && o.SubKind == subKind);
    }
}

[MiscCondition]
public class SanctuaryCondition : ICondition, IDrawableCondition
{
    public string ID => "is";
    public int DisplayPriority => 0;
    public unsafe bool Check(dynamic arg) => TerritoryInfo.Instance()->InSanctuary;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => Loc.Get("SanctuaryCondition.SelectableTooltip");
    public void Draw(CndCfg cndCfg) { }
}

[MiscCondition]
public class ExplorerModeCondition : ICondition
{
    public string ID => "em";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => GameActions.IsInExplorerMode;
}

[MiscCondition]
public class AddonExistsCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "ae";
    public int DisplayPriority => 100;
    public unsafe bool Check(dynamic arg) => arg is string addon && GameActions.GetAddonStructByName(addon) != null;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => Loc.Get("Condition.Advanced");
    public void Draw(CndCfg cndCfg) => DrawAddonInput(cndCfg);
    public dynamic GetDefaultArg(CndCfg cndCfg) => cndCfg.Arg is string ? cndCfg.Arg : string.Empty;

    private void DrawAddonInput(CndCfg cndCfg)
    {
        string addon = cndCfg.Arg is string ? cndCfg.Arg : string.Empty;
        var focusedAddon = GameActions.GetFocusedAddonName();
        if (ImGui.InputTextWithHint("##UIName", focusedAddon, ref addon, 32))
        {
            cndCfg.Arg = addon;
            QoLBarManager.Instance.SaveConfig();
        }

        if (!ImGui.IsItemHovered()) return;

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right) && !string.IsNullOrEmpty(focusedAddon))
        {
            cndCfg.Arg = focusedAddon;
            QoLBarManager.Instance.SaveConfig();
        }

        ImGui.SetTooltip(Loc.Get("Addon.Tooltip"));
    }
}

[MiscCondition]
public class AddonVisibleCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "av";
    public int DisplayPriority => 101;
    public unsafe bool Check(dynamic arg) => arg is string addon && GameActions.GetAddonStructByName(addon) is var atkBase && atkBase != null && atkBase->IsVisible;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => Loc.Get("Condition.Advanced");
    public void Draw(CndCfg cndCfg)
    {
        string addon = cndCfg.Arg is string ? cndCfg.Arg : string.Empty;
        var focusedAddon = GameActions.GetFocusedAddonName();
        if (ImGui.InputTextWithHint("##UIName", focusedAddon, ref addon, 32))
        {
            cndCfg.Arg = addon;
            QoLBarManager.Instance.SaveConfig();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.Get("Addon.Tooltip"));
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => cndCfg.Arg is string ? cndCfg.Arg : string.Empty;
}

[MiscCondition]
public class PluginCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "p";
    public int DisplayPriority => 102;
    public bool Check(dynamic arg) => arg is string plugin && QoLBarManager.HasPlugin(plugin);
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;

    public void Draw(CndCfg cndCfg)
    {
        if (ImGui.BeginCombo("##PluginsList", cndCfg.Arg is string ? cndCfg.Arg : string.Empty))
        {
            var i = 0;
            foreach (var plugin in Framework.DalamudPlugin.InstalledPlugins)
            {
                var name = plugin.InternalName;
                if (!ImGui.Selectable($"{name}##{i++}", cndCfg.Arg == name)) continue;

                cndCfg.Arg = name;
                QoLBarManager.Instance.SaveConfig();
            }

            ImGui.EndCombo();
        }
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => cndCfg.Arg is string ? cndCfg.Arg : string.Empty;
}

public class ConditionFlagCondition : ICondition, IDrawableCondition, IConditionCategory
{
    public const string ConstId = "cf";
    private static readonly Array conditionFlags = Enum.GetValues(typeof(ConditionFlag));

    public string ID => ConstId;
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => Framework.Service<Dalamud.Plugin.Services.ICondition>()[(ConditionFlag)arg];
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        var selected = (ConditionFlag)cndCfg.Arg;
        if (!ImGui.BeginCombo("##Flag", Loc.Get($"ConditionFlag.{selected}", selected.ToString()))) return;

        foreach (ConditionFlag flag in conditionFlags)
        {
            if (!ImGui.Selectable(Loc.Get($"ConditionFlag.{flag}", flag.ToString()), (int)flag == cndCfg.Arg)) continue;

            cndCfg.Arg = (int)flag;
            QoLBarManager.Instance.SaveConfig();
        }
        ImGui.EndCombo();
    }
}

public class ConditionSetCondition : ICondition, IDrawableCondition, IConditionCategory
{
    public const string ConstId = "cs";

    public string ID => ConstId;
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => ConditionManager.CheckConditionSet((int)arg);
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        var sets = QoLBarManager.Instance.Config.ConditionSets;
        var i = (int)cndCfg.Arg;
        if (!ImGui.BeginCombo("##Sets", (i >= 0 && i < sets.Count) ? $"[{i + 1}] {sets[i].Name}" : string.Empty)) return;

        for (var ind = 0; ind < sets.Count; ind++)
        {
            var s = sets[ind];
            if (!ImGui.Selectable($"[{ind + 1}] {s.Name}", ind == i)) continue;

            cndCfg.Arg = ind;
            QoLBarManager.Instance.SaveConfig();
        }
        ImGui.EndCombo();
    }
}

public class JobCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
{
    public string ID => "j";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => Framework.Service<IObjectTable>().LocalPlayer is { } player && player.ClassJob.RowId == (uint)arg;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => Loc.Get("Condition.Advanced");
    public void Draw(CndCfg cndCfg)
    {
        var jobs = Framework.Service<IDataManager>().GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
        var r = jobs.GetRowOrDefault((uint)cndCfg.Arg);
        if (!ImGui.BeginCombo("##Job", r?.Abbreviation.ToString())) return;

        foreach (var j in jobs)
        {
            if (j.RowId == 0) continue;
            if (!ImGui.Selectable(j.Abbreviation.ToString(), j.RowId == cndCfg.Arg)) continue;

            cndCfg.Arg = j.RowId;
            QoLBarManager.Instance.SaveConfig();
        }
        ImGui.EndCombo();
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => Framework.Service<IObjectTable>().LocalPlayer is { } player ? player.ClassJob.RowId : 0;
}

public class RoleCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
{
    public static readonly Dictionary<int, string> RoleDictionary = new()
    {
        [1] = Loc.Get("Role.Tank"),
        [2] = Loc.Get("Role.MeleeDPS"),
        [3] = Loc.Get("Role.RangedDPS"),
        [4] = Loc.Get("Role.Healer"),
        [30] = Loc.Get("Role.DoW"),
        [31] = Loc.Get("Role.DoM"),
        [32] = Loc.Get("Role.DoL"),
        [33] = Loc.Get("Role.DoH")
    };

    public string ID => "r";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => Framework.Service<IObjectTable>().LocalPlayer is { } player
        && ((uint)arg < 30 ? player.ClassJob.ValueNullable?.Role : player.ClassJob.ValueNullable?.ClassJobCategory.RowId) == (uint)arg;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        RoleDictionary.TryGetValue((int)cndCfg.Arg, out string s);
        if (!ImGui.BeginCombo("##Role", s)) return;

        foreach (var (id, name) in RoleDictionary)
        {
            if (!ImGui.Selectable(name, id == cndCfg.Arg)) continue;

            cndCfg.Arg = id;
            QoLBarManager.Instance.SaveConfig();
        }
        ImGui.EndCombo();
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => RoleDictionary.FirstOrDefault(kv => Check(kv.Key)).Key;
}

public class ZoneCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
{
    public string ID => "z";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => Framework.Service<IClientState>().TerritoryType == (ushort)arg;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        static string formatName(Lumina.Excel.Sheets.TerritoryType t) => $"[{t.RowId}] {t.PlaceName.ValueNullable?.Name}";

        var sheet = Framework.Service<IDataManager>().GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
        var preview = sheet.GetRowOrDefault((uint)cndCfg.Arg) is { } current ? formatName(current) : string.Empty;

        if (!ImGuiEx.ExcelSheetCombo(
                "##Zone",
                out Lumina.Excel.Sheets.TerritoryType? territory,
                preview,
                ImGuiComboFlags.None,
                (t, s) => formatName(t).Contains(s, StringComparison.CurrentCultureIgnoreCase),
                t => ImGui.Selectable(formatName(t), (int)cndCfg.Arg == t.RowId)
            )) return;

        cndCfg.Arg = territory.Value.RowId;
        QoLBarManager.Instance.SaveConfig();
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => (int)Framework.Service<IClientState>().TerritoryType;
}

public class CurrentJobPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var jobRow = Framework.Service<IObjectTable>().LocalPlayer?.ClassJob;
        if (jobRow == null) return null;

        var set = new CndSetCfg { Name = jobRow.Value.ValueNullable?.Abbreviation.ExtractText() };
        set.Conditions.Add(new() { ID = new JobCondition().ID, Arg = jobRow.Value.RowId });

        return set;
    }
}

public class CurrentRolePreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var jobRow = Framework.Service<IObjectTable>().LocalPlayer?.ClassJob;
        if (jobRow == null) return null;

        var role = jobRow.Value.ValueNullable?.Role;
        var set = new CndSetCfg { Name = role is > 0 and < 5 ? RoleCondition.RoleDictionary[(int)role] : string.Empty };
        set.Conditions.Add(new() { ID = new RoleCondition().ID, Arg = role });

        return set;
    }
}

public class AllCurrentConditionFlagsPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = Loc.Get("PresetName.ConditionFlagsActive") };
        var condition = Framework.Service<Dalamud.Plugin.Services.ICondition>();

        for (int i = 0; i < condition.MaxEntries; i++)
        {
            if (!condition[i]) continue;
            set.Conditions.Add(new() { ID = ConditionFlagCondition.ConstId, Arg = i });
        }

        return set;
    }
}

public class OutOfCombatPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = Loc.Get("PresetName.OutOfCombat") };
        set.Conditions.Add(new() { ID = ConditionFlagCondition.ConstId, Arg = (int)ConditionFlag.InCombat, Negate = true });
        return set;
    }
}

public class OutofTheWayPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = Loc.Get("PresetName.OutOfTheWay") };
        set.Conditions.Add(new() { ID = ConditionFlagCondition.ConstId, Arg = (int)ConditionFlag.BoundByDuty, Negate = true });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 732, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 763, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 795, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 827, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 920, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 975, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 1055, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = ConditionFlagCondition.ConstId, Arg = (int)ConditionFlag.BetweenAreas, Negate = true });
        set.Conditions.Add(new() { ID = ConditionFlagCondition.ConstId, Arg = (int)ConditionFlag.OccupiedInCutSceneEvent, Negate = true });

        return set;
    }
}
