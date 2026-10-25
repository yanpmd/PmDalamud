namespace Umbra.MoreControls.Conditions;

/// <summary>
/// A single condition type. Implementations are discovered by reflection, so
/// adding a new condition requires nothing more than adding a class.
/// </summary>
internal interface IDisplayCondition
{
    /// <summary>Short, stable identifier used for serialization.</summary>
    string Id { get; }

    /// <summary>Human readable name shown in the condition picker.</summary>
    string Name { get; }

    /// <summary>Evaluates this condition against the current game state.</summary>
    bool Check(ConditionArg arg);

    /// <summary>Short summary of the configured argument, shown in the condition list.</summary>
    string Describe(ConditionArg arg);

    /// <summary>Draws the argument editor for this condition.</summary>
    void DrawEditor(ConditionArg arg);
}

/// <summary>
/// Groups conditions into a section of the condition picker. Applied to the
/// condition class itself.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class ConditionCategoryAttribute(string name, int order = 0) : Attribute
{
    public string Name  { get; } = name;
    public int    Order { get; } = order;
}
