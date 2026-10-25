namespace Umbra.MoreControls.Config;

/// <summary>
/// How a condition is combined with the accumulated result of the preceding
/// conditions in the same set. Mirrors QoLBar's linear (left-to-right) model.
/// </summary>
public enum ConditionJoin
{
    And = 0,
    Or  = 1,
    Xor = 2,
}

/// <summary>
/// Type-safe argument bag. Each condition type documents which fields it uses,
/// which keeps serialized data stable and avoids the dynamic-cast pitfalls of
/// QoLBar's "object Arg" approach.
/// </summary>
public sealed class ConditionArg
{
    /// <summary>Free-form text argument (time ranges, item/addon names, ids).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Primary numeric argument (ids, flags, thresholds).</summary>
    public int Int1 { get; set; }

    /// <summary>Secondary numeric argument (upper bounds).</summary>
    public int Int2 { get; set; }

    /// <summary>Boolean toggle used by conditions that have a "mode".</summary>
    public bool Flag { get; set; }

    public ConditionArg Clone()
    {
        return new() { Text = Text, Int1 = Int1, Int2 = Int2, Flag = Flag };
    }
}

public sealed class ConditionConfig
{
    /// <summary>Condition type id, e.g. "cf", "job", "zone".</summary>
    public string Id { get; set; } = "cf";

    public bool Negate { get; set; }

    public ConditionJoin Join { get; set; } = ConditionJoin.And;

    public ConditionArg Arg { get; set; } = new();
}

public sealed class ConditionSetConfig
{
    /// <summary>Stable identifier used by bindings. Never changes on rename/reorder.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public List<ConditionConfig> Conditions { get; set; } = [];
}

/// <summary>
/// Links a target (a widget instance or a toolbar panel) to a condition set.
/// </summary>
public sealed class ConditionBinding
{
    /// <summary>Condition set id, or null/empty when the target is unbound.</summary>
    public string? SetId { get; set; }

    /// <summary>Inverts the evaluated result (show only when the set is false).</summary>
    public bool Invert { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsBound => !string.IsNullOrEmpty(SetId);
}

public sealed class MoreControlsConfig
{
    public int Version { get; set; } = 1;

    public List<ConditionSetConfig> Sets { get; set; } = [];

    /// <summary>Widget instance GUID -> binding.</summary>
    public Dictionary<string, ConditionBinding> Widgets { get; set; } = [];

    /// <summary>Panel id ("Toolbar", "Left", "Center", "Right") -> binding.</summary>
    public Dictionary<string, ConditionBinding> Panels { get; set; } = [];

    public ConditionSetConfig? FindSet(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        return Sets.FirstOrDefault(s => s.Id == id);
    }

    public ConditionSetConfig? FindSetByName(string name)
    {
        return Sets.FirstOrDefault(s => s.Name == name);
    }

    public ConditionSetConfig AddSet(string name)
    {
        var set = new ConditionSetConfig { Name = name };
        Sets.Add(set);

        return set;
    }

    public void RemoveSet(ConditionSetConfig set)
    {
        Sets.Remove(set);

        foreach (ConditionBinding binding in Widgets.Values) {
            if (binding.SetId == set.Id) binding.SetId = null;
        }

        foreach (ConditionBinding binding in Panels.Values) {
            if (binding.SetId == set.Id) binding.SetId = null;
        }
    }
}
