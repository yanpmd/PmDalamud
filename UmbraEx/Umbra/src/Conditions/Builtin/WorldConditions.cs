using Umbra.Windows.Components;

namespace Umbra.Conditions.Builtin;

[ConditionCategory("World", 20)]
internal sealed class ZoneCondition : IDisplayCondition
{
    public string Id   => "zone";
    public string Name => "所在区域 (TerritoryType)";

    public bool Check(ConditionArg arg)
    {
        return ConditionServices.ClientState?.TerritoryType == (uint)arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return ConditionServices.GetZoneName((uint)arg.Int1);
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var input = new IntegerInputNode($"zone-id{idSuffix}", arg.Int1, 0, 99999, "区域 ID", null, 0);
        input.OnValueChanged += value => {
            arg.Int1 = value;
            onChanged();
        };

        var useCurrent = new ButtonNode($"zone-current{idSuffix}", "使用当前区域", null, true, true);
        useCurrent.OnClick += _ => {
            arg.Int1 = (int)(ConditionServices.ClientState?.TerritoryType ?? 0);
            onChanged();
        };

        container.AppendChild(input);
        container.AppendChild(useCurrent);
    }
}

[ConditionCategory("World", 20)]
internal sealed class PartyCountCondition : IDisplayCondition
{
    public string Id   => "party";
    public string Name => "队伍人数";

    public bool Check(ConditionArg arg)
    {
        int count = ConditionServices.PartyList?.Length ?? 0;

        return arg.Flag ? count < arg.Int1 : count >= arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return arg.Flag ? $"少于 {arg.Int1} 人" : $"至少 {arg.Int1} 人";
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var count = new IntegerInputNode($"party-count{idSuffix}", arg.Int1, 1, 24, "人数", null, 0);
        count.OnValueChanged += value => {
            arg.Int1 = value;
            onChanged();
        };

        var less = new CheckboxNode($"party-less{idSuffix}", arg.Flag, "少于该人数时成立");
        less.OnValueChanged += value => {
            arg.Flag = value;
            onChanged();
        };

        container.AppendChild(count);
        container.AppendChild(less);
    }
}

/// <summary>Shared HH:mm-HH:mm range parsing, supporting ranges across midnight.</summary>
internal static class TimeRange
{
    public static bool Contains(string spec, int minutesOfDay)
    {
        if (string.IsNullOrWhiteSpace(spec)) return true;

        string[] parts = spec.Split('-', StringSplitOptions.TrimEntries);

        if (parts.Length != 2) return true;
        if (!TryParse(parts[0], out int from) || !TryParse(parts[1], out int to)) return true;

        return from <= to
            ? minutesOfDay >= from && minutesOfDay <= to
            : minutesOfDay >= from || minutesOfDay <= to;
    }

    private static bool TryParse(string value, out int minutes)
    {
        minutes = 0;

        string[] parts = value.Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out int hour) || !int.TryParse(parts[1], out int minute)) return false;

        minutes = Math.Clamp(hour, 0, 23) * 60 + Math.Clamp(minute, 0, 59);

        return true;
    }
}

[ConditionCategory("World", 20)]
internal sealed class EorzeaTimeCondition : IDisplayCondition
{
    public string Id   => "et";
    public string Name => "艾欧泽亚时间";

    public bool Check(ConditionArg arg)
    {
        // One Eorzea day is 70 real minutes (1440 Eorzea minutes).
        long utcSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int  minutes    = (int)(utcSeconds * 24 / 70 % 1440);

        return TimeRange.Contains(arg.Text, minutes);
    }

    public string Describe(ConditionArg arg)
    {
        return string.IsNullOrWhiteSpace(arg.Text) ? "全天" : arg.Text;
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var node = new StringInputNode($"et{idSuffix}", arg.Text, 32, "时间段 (HH:mm-HH:mm)", "留空表示全天。", 0, true);

        node.OnValueChanged += value => {
            arg.Text = value;
            onChanged();
        };

        container.AppendChild(node);
    }
}

[ConditionCategory("World", 20)]
internal sealed class LocalTimeCondition : IDisplayCondition
{
    public string Id   => "lt";
    public string Name => "本地时间";

    public bool Check(ConditionArg arg)
    {
        DateTime now = DateTime.Now;

        return TimeRange.Contains(arg.Text, now.Hour * 60 + now.Minute);
    }

    public string Describe(ConditionArg arg)
    {
        return string.IsNullOrWhiteSpace(arg.Text) ? "全天" : arg.Text;
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var node = new StringInputNode($"lt{idSuffix}", arg.Text, 32, "时间段 (HH:mm-HH:mm)", "留空表示全天。", 0, true);

        node.OnValueChanged += value => {
            arg.Text = value;
            onChanged();
        };

        container.AppendChild(node);
    }
}

[ConditionCategory("Conditions", 30)]
internal sealed class ConditionSetRefCondition : IDisplayCondition
{
    public string Id   => "set";
    public string Name => "引用其他条件组";

    public bool Check(ConditionArg arg)
    {
        try {
            return Framework.Service<ConditionSetManager>().Evaluate(arg.Text);
        } catch {
            return false;
        }
    }

    public string Describe(ConditionArg arg)
    {
        try {
            ConditionSetConfig? set = Framework.Service<ConditionSetManager>().Get(arg.Text);

            return set is null ? "（未选择）" : set.Name;
        } catch {
            return "（未选择）";
        }
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        ConditionSetManager? manager = null;

        try {
            manager = Framework.Service<ConditionSetManager>();
        } catch {
            // Ignored; the selector simply stays empty.
        }

        var options = manager?.All
                             .Select(set => (set.Id, string.IsNullOrWhiteSpace(set.Name) ? "(unnamed)" : set.Name))
                             .ToList() ?? [];

        if (options.Count == 0) {
            var hint = new Node();

            hint.AppendChild(new Node { NodeValue = "还没有可引用的条件组。" });

            container.AppendChild(hint);

            return;
        }

        container.AppendChild(
            SelectHelper.Create(
                $"set-ref{idSuffix}",
                options,
                arg.Text,
                value => {
                    arg.Text = value;
                    onChanged();
                }
            )
        );
    }
}
