using Dalamud.Game.ClientState.Conditions;

namespace QoLBar.Conditions;

public class CurrentJobPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var jobRow = DalamudApi.ObjectTable.LocalPlayer?.ClassJob;
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
        var jobRow = DalamudApi.ObjectTable.LocalPlayer?.ClassJob;
        if (jobRow == null) return null;

        var role = jobRow.Value.ValueNullable?.Role;
        var set = new CndSetCfg { Name = role is > 0 and < 5 ? RoleCondition.roleDictionary[(int)role] : string.Empty };
        set.Conditions.Add(new() { ID = new RoleCondition().ID, Arg = role });

        return set;
    }
}

public class AllCurrentConditionFlagsPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = Localization.Get("PresetName.ConditionFlagsActive") };

        for (int i = 0; i < DalamudApi.Condition.MaxEntries; i++)
        {
            if (!DalamudApi.Condition[i]) continue;
            set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = i });
        }

        return set;
    }
}

public class OutOfCombatPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = Localization.Get("PresetName.OutOfCombat") };
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.InCombat, Negate = true });
        return set;
    }
}

public class OutofTheWayPreset : IConditionSetPreset
{
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = Localization.Get("PresetName.OutOfTheWay") };
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.BoundByDuty, Negate = true });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 732, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 763, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 795, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 827, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 920, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 975, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 1055, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.BetweenAreas, Negate = true });
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.OccupiedInCutSceneEvent, Negate = true });

        return set;
    }
}