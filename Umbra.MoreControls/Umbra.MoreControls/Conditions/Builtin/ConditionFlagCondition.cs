using Dalamud.Game.ClientState.Conditions;

namespace Umbra.MoreControls.Conditions.Builtin;

/// <summary>
/// Generic condition that checks any Dalamud <see cref="ConditionFlag"/>. This
/// single condition already covers most QoLBar use cases (combat, duty, PvP,
/// cutscenes, mounting, gathering, and so on).
/// </summary>
[ConditionCategory("游戏状态", 0)]
internal sealed class ConditionFlagCondition : IDisplayCondition
{
    public string Id   => "cf";
    public string Name => "游戏状态标记 (ConditionFlag)";

    /// <summary>Flags that are worth surfacing at the top of the list.</summary>
    private static readonly (string Name, string Text)[] KnownFlags = [
        ("InCombat", "战斗中"),
        ("BoundByDuty", "任务中 (BoundByDuty)"),
        ("BoundByDuty56", "副本中 (BoundByDuty56)"),
        ("BoundByDuty95", "副本中 (BoundByDuty95)"),
        ("WatchingCutscene", "过场动画中"),
        ("WatchingCutscene78", "过场动画中 (78)"),
        ("OccupiedInCutSceneEvent", "剧情演出中"),
        ("OccupiedInEvent", "事件进行中"),
        ("OccupiedInQuestEvent", "任务事件中"),
        ("Occupied", "被占用 (Occupied)"),
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
        ("InSanctuary", "安全区 (不适用全局)"),
        ("PvP", "PvP 状态"),
        ("EditingHud", "编辑界面中"),
        ("GPose", "集体动作中"),
    ];

    private static readonly Dictionary<string, string> KnownLookup =
        KnownFlags.ToDictionary(entry => entry.Name, entry => entry.Text);

    private static List<(ConditionFlag Flag, string Label)>? _entries;

    private string _filter = string.Empty;

    public bool Check(ConditionArg arg)
    {
        return Services.HasCondition((ConditionFlag)arg.Int1);
    }

    public string Describe(ConditionArg arg)
    {
        return GetLabel((ConditionFlag)arg.Int1);
    }

    public static string GetLabel(ConditionFlag flag)
    {
        string name = flag.ToString();

        return KnownLookup.TryGetValue(name, out string? text) ? $"{text} · {name}" : name;
    }

    public void DrawEditor(ConditionArg arg)
    {
        var current = (ConditionFlag)arg.Int1;

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##cf-filter", ref _filter, 64);

        ImGui.SameLine();
        ImGui.TextDisabled("搜索");

        ImGui.SetNextItemWidth(-1);

        if (ImGui.BeginCombo("##cf-value", GetLabel(current))) {
            foreach ((ConditionFlag flag, string label) in GetEntries()) {
                if (_filter.Length > 0 && label.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (ImGui.Selectable(label, flag == current)) {
                    arg.Int1 = (int)flag;
                }
            }

            ImGui.EndCombo();
        }
    }

    private static List<(ConditionFlag Flag, string Label)> GetEntries()
    {
        if (_entries is not null) return _entries;

        var list = new List<(ConditionFlag Flag, string Label)>();

        // Well-known flags first, in the curated order above.
        foreach ((string name, _) in KnownFlags) {
            if (!Enum.TryParse(name, out ConditionFlag flag)) continue;
            if (list.Any(e => e.Flag == flag)) continue;

            list.Add((flag, GetLabel(flag)));
        }

        // Then everything else that carries an actual name (skip UnkN noise).
        foreach (ConditionFlag flag in Enum.GetValues<ConditionFlag>().Distinct().OrderBy(f => (int)f)) {
            string name = flag.ToString();

            if (name.StartsWith("Unk", StringComparison.Ordinal)) continue;
            if (name == "None") continue;
            if (list.Any(e => e.Flag == flag)) continue;

            list.Add((flag, GetLabel(flag)));
        }

        _entries = list;

        return list;
    }
}
