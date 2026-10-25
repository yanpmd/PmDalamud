namespace Umbra.MoreControls.Conditions.Builtin;

[ConditionCategory("世界", 20)]
internal sealed class ZoneCondition : IDisplayCondition
{
    public string Id   => "zone";
    public string Name => "所在区域 (TerritoryType)";

    public bool Check(ConditionArg arg)
    {
        return Services.ClientState?.TerritoryType == (uint)arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return Services.GetZoneName((uint)arg.Int1);
    }

    public void DrawEditor(ConditionArg arg)
    {
        ImGui.SetNextItemWidth(140);
        int id = arg.Int1;

        if (ImGui.InputInt("区域 ID", ref id)) arg.Int1 = id;

        ImGui.SameLine();

        if (ImGui.Button("使用当前区域")) {
            IClientState? state = Services.ClientState;

            if (state is not null) arg.Int1 = (int)state.TerritoryType;
        }

        ImGui.SameLine();
        ImGui.TextDisabled(Services.GetZoneName((uint)arg.Int1));
    }
}

[ConditionCategory("世界", 20)]
internal sealed class PartyCountCondition : IDisplayCondition
{
    public string Id   => "party";
    public string Name => "队伍人数";

    public bool Check(ConditionArg arg)
    {
        int count = Services.PartyList?.Length ?? 0;

        return arg.Flag ? count < arg.Int1 : count >= arg.Int1;
    }

    public string Describe(ConditionArg arg)
    {
        return arg.Flag ? $"少于 {arg.Int1} 人" : $"至少 {arg.Int1} 人";
    }

    public void DrawEditor(ConditionArg arg)
    {
        ImGui.SetNextItemWidth(120);
        int count = arg.Int1;

        if (ImGui.InputInt("人数", ref count)) arg.Int1 = Math.Clamp(count, 1, 24);

        ImGui.SameLine();

        bool less = arg.Flag;

        if (ImGui.Checkbox("少于", ref less)) arg.Flag = less;
    }
}

/// <summary>Shared HH:mm-HH:mm range parsing (supports ranges that wrap midnight).</summary>
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
        if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return false;

        minutes = (Math.Clamp(h, 0, 23) * 60) + Math.Clamp(m, 0, 59);

        return true;
    }
}

[ConditionCategory("世界", 20)]
internal sealed class EorzeaTimeCondition : IDisplayCondition
{
    public string Id   => "et";
    public string Name => "艾欧泽亚时间";

    public bool Check(ConditionArg arg)
    {
        // 1 Eorzea day = 70 real minutes = 1440 Eorzea minutes.
        long utcSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int  minutes    = (int)((utcSeconds * 24 / 70) % 1440);

        return TimeRange.Contains(arg.Text, minutes);
    }

    public string Describe(ConditionArg arg)
    {
        return string.IsNullOrWhiteSpace(arg.Text) ? "全天" : arg.Text;
    }

    public void DrawEditor(ConditionArg arg)
    {
        string text = arg.Text;

        ImGui.SetNextItemWidth(160);

        if (ImGui.InputText("##et", ref text, 32)) arg.Text = text;

        ImGui.SameLine();
        ImGui.TextDisabled("格式 08:00-18:00");
    }
}

[ConditionCategory("世界", 20)]
internal sealed class LocalTimeCondition : IDisplayCondition
{
    public string Id   => "lt";
    public string Name => "本地时间";

    public bool Check(ConditionArg arg)
    {
        DateTime now = DateTime.Now;

        return TimeRange.Contains(arg.Text, (now.Hour * 60) + now.Minute);
    }

    public string Describe(ConditionArg arg)
    {
        return string.IsNullOrWhiteSpace(arg.Text) ? "全天" : arg.Text;
    }

    public void DrawEditor(ConditionArg arg)
    {
        string text = arg.Text;

        ImGui.SetNextItemWidth(160);

        if (ImGui.InputText("##lt", ref text, 32)) arg.Text = text;

        ImGui.SameLine();
        ImGui.TextDisabled("格式 08:00-18:00");
    }
}

[ConditionCategory("条件组", 30)]
internal sealed class ConditionSetRefCondition : IDisplayCondition
{
    public string Id   => "set";
    public string Name => "引用其他条件组";

    public bool Check(ConditionArg arg)
    {
        return ConditionManager.EvaluateSetById(arg.Text);
    }

    public string Describe(ConditionArg arg)
    {
        ConditionSetConfig? set = ConfigStore.Config.FindSet(arg.Text);

        return set is null ? "（未选择）" : set.Name;
    }

    public void DrawEditor(ConditionArg arg)
    {
        ConditionSetConfig? current = ConfigStore.Config.FindSet(arg.Text);
        string              label   = current is null ? "（未选择）" : current.Name;

        ImGui.SetNextItemWidth(-1);

        if (!ImGui.BeginCombo("##setref", label)) return;

        foreach (ConditionSetConfig set in ConfigStore.Config.Sets) {
            if (ImGui.Selectable(set.Name, set.Id == arg.Text)) {
                arg.Text = set.Id;
                ConfigStore.MarkDirty();
            }
        }

        ImGui.EndCombo();
    }
}
