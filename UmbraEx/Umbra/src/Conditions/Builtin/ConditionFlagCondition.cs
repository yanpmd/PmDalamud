using Dalamud.Game.ClientState.Conditions;

namespace Umbra.Conditions.Builtin;

/// <summary>
/// Generic condition that checks any Dalamud <see cref="ConditionFlag"/>.
/// This single condition covers most "auto hide" use cases: combat, duties,
/// PvP, cutscenes, mounting, gathering and so on.
/// </summary>
[ConditionCategory("Game State", 0)]
internal sealed class ConditionFlagCondition : IDisplayCondition
{
    public string Id   => "cf";
    public string Name => "游戏状态 (ConditionFlag)";

    /// <summary>Flags surfaced at the top of the list, in this order.</summary>
    private static readonly (string Name, string Text)[] KnownFlags = [
        ("InCombat", "战斗中"),
        ("BoundByDuty", "任务中"),
        ("BoundByDuty56", "副本中"),
        ("BoundByDuty95", "副本中 (深层)"),
        ("WatchingCutscene", "过场动画中"),
        ("WatchingCutscene78", "过场动画中 (78)"),
        ("OccupiedInCutSceneEvent", "剧情演出中"),
        ("OccupiedInEvent", "事件进行中"),
        ("OccupiedInQuestEvent", "任务事件中"),
        ("Occupied", "界面占用中"),
        ("BetweenAreas", "区域切换中"),
        ("BetweenAreas51", "区域切换中 (51)"),
        ("Mounted", "骑乘中"),
        ("RidingPillion", "双人坐骑中"),
        ("Casting", "咏唱中"),
        ("Diving", "潜水/下潜中"),
        ("Jumping", "跳跃中"),
        ("Fishing", "钓鱼中"),
        ("Gathering", "采集中"),
        ("NormalConditions", "正常状态"),
        ("PvP", "PvP"),
        ("GPose", "集体动作中"),
    ];

    private static List<(string Id, string Label)>? _options;

    public bool Check(ConditionArg arg)
    {
        if (!Enum.TryParse(arg.Text, out ConditionFlag flag)) return false;

        return ConditionServices.HasCondition(flag);
    }

    public string Describe(ConditionArg arg)
    {
        return GetLabel(arg.Text);
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        container.AppendChild(
            SelectHelper.Create(
                $"cf{idSuffix}",
                GetOptions(),
                arg.Text,
                value => {
                    arg.Text = value;
                    onChanged();
                }
            )
        );
    }

    private static string GetLabel(string? name)
    {
        foreach ((string flagName, string text) in KnownFlags) {
            if (flagName == name) return $"{text} · {flagName}";
        }

        return string.IsNullOrEmpty(name) ? "（未选择）" : name;
    }

    private static List<(string Id, string Label)> GetOptions()
    {
        if (_options is not null) return _options;

        var options = new List<(string Id, string Label)>();

        foreach ((string name, _) in KnownFlags) {
            if (!Enum.TryParse(name, out ConditionFlag _)) continue;
            if (options.Any(entry => entry.Id == name)) continue;

            options.Add((name, GetLabel(name)));
        }

        foreach (ConditionFlag flag in Enum.GetValues<ConditionFlag>().Distinct().OrderBy(value => (int)value)) {
            string name = flag.ToString();

            if (name.StartsWith("Unk", StringComparison.Ordinal) || name == "None") continue;
            if (options.Any(entry => entry.Id == name)) continue;

            options.Add((name, name));
        }

        _options = options;

        return options;
    }
}
