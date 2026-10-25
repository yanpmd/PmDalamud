namespace Umbra.MoreControls.Conditions.Builtin;

[ConditionCategory("角色", 10)]
internal sealed class JobCondition : IDisplayCondition
{
    public string Id   => "job";
    public string Name => "当前职业";

    public bool Check(ConditionArg arg)
    {
        return JobData.GetCurrentJobId() == (byte)arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return JobData.GetJobName((byte)arg.Int1);
    }

    public void DrawEditor(ConditionArg arg)
    {
        string label = JobData.GetJobName((byte)arg.Int1);

        ImGui.SetNextItemWidth(-1);

        if (!ImGui.BeginCombo("##job", label)) return;

        foreach ((byte id, string name, string abbr, _) in JobData.Jobs) {
            if (ImGui.Selectable($"{name} ({abbr})", id == arg.Int1)) {
                arg.Int1 = id;
            }
        }

        ImGui.EndCombo();
    }
}

[ConditionCategory("角色", 10)]
internal sealed class JobRoleCondition : IDisplayCondition
{
    public string Id   => "role";
    public string Name => "当前职能";

    private static readonly JobRole[] Roles = [
        JobRole.Tank,
        JobRole.Healer,
        JobRole.Melee,
        JobRole.Ranged,
        JobRole.Caster,
        JobRole.Crafter,
        JobRole.Gatherer,
    ];

    public bool Check(ConditionArg arg)
    {
        return JobData.GetRole(JobData.GetCurrentJobId()) == (JobRole)arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return JobData.GetRoleName((JobRole)arg.Int1);
    }

    public void DrawEditor(ConditionArg arg)
    {
        ImGui.SetNextItemWidth(-1);

        if (!ImGui.BeginCombo("##role", JobData.GetRoleName((JobRole)arg.Int1))) return;

        foreach (JobRole role in Roles) {
            if (ImGui.Selectable(JobData.GetRoleName(role), (int)role == arg.Int1)) {
                arg.Int1 = (int)role;
            }
        }

        ImGui.EndCombo();
    }
}

[ConditionCategory("角色", 10)]
internal sealed class LevelCondition : IDisplayCondition
{
    public string Id   => "level";
    public string Name => "等级范围";

    public bool Check(ConditionArg arg)
    {
        int level = Services.Player?.Level ?? 0;
        int min   = Math.Min(arg.Int1, arg.Int2);
        int max   = Math.Max(arg.Int1, arg.Int2);

        return level >= min && level <= max;
    }

    public string Describe(ConditionArg arg)
    {
        return $"{Math.Min(arg.Int1, arg.Int2)} - {Math.Max(arg.Int1, arg.Int2)} 级";
    }

    public void DrawEditor(ConditionArg arg)
    {
        ImGui.SetNextItemWidth(80);
        int min = arg.Int1;

        if (ImGui.InputInt("最低等级", ref min)) arg.Int1 = Math.Clamp(min, 0, 100);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        int max = arg.Int2;

        if (ImGui.InputInt("最高等级", ref max)) arg.Int2 = Math.Clamp(max, 0, 100);
    }
}

[ConditionCategory("角色", 10)]
internal sealed class WeaponDrawnCondition : IDisplayCondition
{
    public string Id   => "weapon";
    public string Name => "武器拔出状态";

    public bool Check(ConditionArg arg)
    {
        bool drawn = Services.Player?.IsWeaponDrawn ?? false;

        return arg.Flag ? !drawn : drawn;
    }

    public string Describe(ConditionArg arg)
    {
        return arg.Flag ? "武器收起" : "武器拔出";
    }

    public void DrawEditor(ConditionArg arg)
    {
        bool hide = arg.Flag;

        if (ImGui.Checkbox("反转（判定为收起武器时）", ref hide)) arg.Flag = hide;
    }
}
