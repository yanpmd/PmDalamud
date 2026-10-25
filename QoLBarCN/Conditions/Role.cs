using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace QoLBar.Conditions;

public class RoleCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
{
    public static readonly Dictionary<int, string> roleDictionary = new()
    {
        [1] = Localization.Get("Role.Tank"),
        [2] = Localization.Get("Role.MeleeDPS"),
        [3] = Localization.Get("Role.RangedDPS"),
        [4] = Localization.Get("Role.Healer"),
        [30] = Localization.Get("Role.DoW"),
        [31] = Localization.Get("Role.DoM"),
        [32] = Localization.Get("Role.DoL"),
        [33] = Localization.Get("Role.DoH")
    };

    public string ID => "r";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => DalamudApi.ObjectTable.LocalPlayer is { } player
        && ((uint)arg < 30 ? player.ClassJob.ValueNullable?.Role : player.ClassJob.ValueNullable?.ClassJobCategory.RowId) == (uint)arg;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        roleDictionary.TryGetValue((int)cndCfg.Arg, out string s);
        if (!ImGui.BeginCombo("##Role", s)) return;

        foreach (var (id, name) in roleDictionary)
        {
            if (!ImGui.Selectable(name, id == cndCfg.Arg)) continue;

            cndCfg.Arg = id;
            QoLBar.Config.Save();
        }
        ImGui.EndCombo();
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => roleDictionary.FirstOrDefault(kv => Check(kv.Key)).Key;
}