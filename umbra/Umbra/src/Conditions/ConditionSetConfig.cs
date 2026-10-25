namespace Umbra.Conditions;

/// <summary>
/// How a condition is combined with the accumulated result of the preceding
/// conditions in the same set.
/// </summary>
public enum ConditionJoin
{
    And = 0,
    Or  = 1,
    Xor = 2,
}

/// <summary>
/// Argument bag for a single condition. Each condition type documents which
/// fields it uses.
/// </summary>
[Serializable]
public sealed class ConditionArg
{
    /// <summary>Free-form text argument (time ranges, ids).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Primary numeric argument (flag value, id, threshold).</summary>
    public int Int1 { get; set; }

    /// <summary>Secondary numeric argument (upper bounds).</summary>
    public int Int2 { get; set; }

    /// <summary>Boolean toggle used by conditions that have a mode.</summary>
    public bool Flag { get; set; }

    public ConditionArg Clone()
    {
        return new() { Text = Text, Int1 = Int1, Int2 = Int2, Flag = Flag };
    }
}

[Serializable]
public sealed class ConditionConfig
{
    /// <summary>Condition type id, e.g. "cf", "job", "zone".</summary>
    public string Id { get; set; } = "cf";

    public bool Negate { get; set; }

    public ConditionJoin Join { get; set; } = ConditionJoin.And;

    public ConditionArg Arg { get; set; } = new();
}

/// <summary>
/// A named, ordered list of conditions. Evaluated left-to-right.
/// </summary>
[Serializable]
public sealed class ConditionSetConfig
{
    /// <summary>Stable identifier used by widget bindings.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public List<ConditionConfig> Conditions { get; set; } = [];

    public void Update(ConditionSetConfig other)
    {
        Name       = other.Name;
        Conditions = other.Conditions;
    }
}
