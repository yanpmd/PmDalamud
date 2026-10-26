using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;

namespace QoLBar.Conditions;

public class ConditionFlagCondition : ICondition, IDrawableCondition, IConditionCategory
{
    public const string constID = "cf";
    private static readonly Array conditionFlags = Enum.GetValues(typeof(ConditionFlag));

    public string ID => constID;
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => DalamudApi.Condition[(ConditionFlag)arg];
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        var selected = (ConditionFlag)cndCfg.Arg;
        if (!ImGui.BeginCombo("##Flag", Localization.Get($"ConditionFlag.{selected}", selected.ToString()))) return;

        foreach (ConditionFlag flag in conditionFlags)
        {
            if (!ImGui.Selectable(Localization.Get($"ConditionFlag.{flag}", flag.ToString()), (int)flag == cndCfg.Arg)) continue;

            cndCfg.Arg = (int)flag;
            QoLBar.Config.Save();
        }
        ImGui.EndCombo();
    }
}