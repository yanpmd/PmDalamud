using Umbra.Windows.Components;

namespace Umbra.Conditions.Builtin;

internal enum JobRole
{
    None     = 0,
    Tank     = 1,
    Healer   = 2,
    Melee    = 3,
    Ranged   = 4,
    Caster   = 5,
    Crafter  = 6,
    Gatherer = 7,
}

internal static class JobData
{
    /// <summary>ClassJob row id, display name, abbreviation and role.</summary>
    public static readonly (byte Id, string Name, string Abbr, JobRole Role)[] Jobs = [
        (0, "冒险者", "ADV", JobRole.None),
        (1, "剑术师", "GLA", JobRole.Tank),
        (2, "格斗家", "PGL", JobRole.Melee),
        (3, "斧术师", "MRD", JobRole.Tank),
        (4, "枪术师", "LNC", JobRole.Melee),
        (5, "弓术师", "ARC", JobRole.Ranged),
        (6, "幻术师", "CNJ", JobRole.Healer),
        (7, "咒术师", "THM", JobRole.Caster),
        (8, "木工", "CRP", JobRole.Crafter),
        (9, "锻铁匠", "BSM", JobRole.Crafter),
        (10, "铸甲匠", "ARM", JobRole.Crafter),
        (11, "雕金匠", "GSM", JobRole.Crafter),
        (12, "制革匠", "LTW", JobRole.Crafter),
        (13, "裁衣匠", "WVR", JobRole.Crafter),
        (14, "炼金术士", "ALC", JobRole.Crafter),
        (15, "烹调师", "CUL", JobRole.Crafter),
        (16, "采矿工", "MIN", JobRole.Gatherer),
        (17, "园艺工", "BTN", JobRole.Gatherer),
        (18, "捕鱼人", "FSH", JobRole.Gatherer),
        (19, "骑士", "PLD", JobRole.Tank),
        (20, "武僧", "MNK", JobRole.Melee),
        (21, "战士", "WAR", JobRole.Tank),
        (22, "龙骑士", "DRG", JobRole.Melee),
        (23, "吟游诗人", "BRD", JobRole.Ranged),
        (24, "白魔法师", "WHM", JobRole.Healer),
        (25, "黑魔法师", "BLM", JobRole.Caster),
        (26, "秘术师", "ACN", JobRole.Caster),
        (27, "召唤师", "SMN", JobRole.Caster),
        (28, "学者", "SCH", JobRole.Healer),
        (29, "双剑师", "ROG", JobRole.Melee),
        (30, "忍者", "NIN", JobRole.Melee),
        (31, "机工士", "MCH", JobRole.Ranged),
        (32, "暗黑骑士", "DRK", JobRole.Tank),
        (33, "占星术士", "AST", JobRole.Healer),
        (34, "武士", "SAM", JobRole.Melee),
        (35, "赤魔法师", "RDM", JobRole.Caster),
        (36, "青魔法师", "BLU", JobRole.Caster),
        (37, "绝枪战士", "GNB", JobRole.Tank),
        (38, "舞者", "DNC", JobRole.Ranged),
        (39, "钐镰客", "RPR", JobRole.Melee),
        (40, "贤者", "SGE", JobRole.Healer),
        (41, "蛇武", "VPR", JobRole.Melee),
        (42, "绘灵", "PCT", JobRole.Caster),
    ];

    public static byte CurrentJobId
    {
        get {
            try {
                return ConditionServices.Player?.JobId ?? 0;
            } catch {
                return 0;
            }
        }
    }

    public static string GetJobName(byte jobId)
    {
        foreach ((byte id, string name, string abbr, _) in Jobs) {
            if (id == jobId) return $"{name} ({abbr})";
        }

        return $"Job #{jobId}";
    }

    public static JobRole GetRole(byte jobId)
    {
        foreach ((byte id, _, _, JobRole role) in Jobs) {
            if (id == jobId) return role;
        }

        return JobRole.None;
    }

    public static string GetRoleName(JobRole role)
    {
        return role switch {
            JobRole.Tank     => "坦克",
            JobRole.Healer   => "治疗",
            JobRole.Melee    => "近战物理",
            JobRole.Ranged   => "远程物理",
            JobRole.Caster   => "远程魔法",
            JobRole.Crafter  => "生产职业",
            JobRole.Gatherer => "采集职业",
            _                => "无",
        };
    }

    public static IEnumerable<(string Id, string Label)> GetJobOptions()
    {
        return Jobs.Select(job => (job.Id.ToString(), GetJobName(job.Id)));
    }

    public static IEnumerable<(string Id, string Label)> GetRoleOptions()
    {
        return new[] {
            JobRole.Tank,
            JobRole.Healer,
            JobRole.Melee,
            JobRole.Ranged,
            JobRole.Caster,
            JobRole.Crafter,
            JobRole.Gatherer,
        }.Select(role => (((int)role).ToString(), GetRoleName(role)));
    }
}

[ConditionCategory("Character", 10)]
internal sealed class JobCondition : IDisplayCondition
{
    public string Id   => "job";
    public string Name => "当前职业";

    public bool Check(ConditionArg arg)
    {
        return JobData.CurrentJobId == (byte)arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return JobData.GetJobName((byte)arg.Int1);
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        container.AppendChild(
            SelectHelper.Create(
                $"job{idSuffix}",
                JobData.GetJobOptions(),
                arg.Int1.ToString(),
                value => {
                    if (byte.TryParse(value, out byte jobId)) arg.Int1 = jobId;
                    onChanged();
                }
            )
        );
    }
}

[ConditionCategory("Character", 10)]
internal sealed class JobRoleCondition : IDisplayCondition
{
    public string Id   => "role";
    public string Name => "当前职能";

    public bool Check(ConditionArg arg)
    {
        return JobData.GetRole(JobData.CurrentJobId) == (JobRole)arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return JobData.GetRoleName((JobRole)arg.Int1);
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        container.AppendChild(
            SelectHelper.Create(
                $"role{idSuffix}",
                JobData.GetRoleOptions(),
                arg.Int1.ToString(),
                value => {
                    if (int.TryParse(value, out int role)) arg.Int1 = role;
                    onChanged();
                }
            )
        );
    }
}

[ConditionCategory("Character", 10)]
internal sealed class LevelCondition : IDisplayCondition
{
    public string Id   => "level";
    public string Name => "等级范围";

    public bool Check(ConditionArg arg)
    {
        int level = ConditionServices.Player?.Level ?? 0;
        int min   = Math.Min(arg.Int1, arg.Int2);
        int max   = Math.Max(arg.Int1, arg.Int2);

        return level >= min && level <= max;
    }

    public string Describe(ConditionArg arg)
    {
        return $"{Math.Min(arg.Int1, arg.Int2)} - {Math.Max(arg.Int1, arg.Int2)} 级";
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var min = new IntegerInputNode($"level-min{idSuffix}", arg.Int1, 0, 100, "最低等级", null, 0);
        min.OnValueChanged += value => {
            arg.Int1 = value;
            onChanged();
        };

        var max = new IntegerInputNode($"level-max{idSuffix}", arg.Int2, 0, 100, "最高等级", null, 0);
        max.OnValueChanged += value => {
            arg.Int2 = value;
            onChanged();
        };

        container.AppendChild(min);
        container.AppendChild(max);
    }
}

[ConditionCategory("Character", 10)]
internal sealed class WeaponDrawnCondition : IDisplayCondition
{
    public string Id   => "weapon";
    public string Name => "武器拔出状态";

    public bool Check(ConditionArg arg)
    {
        bool drawn = ConditionServices.Player?.IsWeaponDrawn ?? false;

        return arg.Flag ? !drawn : drawn;
    }

    public string Describe(ConditionArg arg)
    {
        return arg.Flag ? "武器收起" : "武器拔出";
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var node = new CheckboxNode($"weapon-invert{idSuffix}", arg.Flag, "反转（收起武器时成立）");

        node.OnValueChanged += value => {
            arg.Flag = value;
            onChanged();
        };

        container.AppendChild(node);
    }
}
