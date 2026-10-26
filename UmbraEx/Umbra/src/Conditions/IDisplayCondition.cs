namespace Umbra.Conditions;

/// <summary>
/// A single condition type. Implementations are discovered by reflection, so
/// adding a new condition requires nothing more than adding a class.
/// </summary>
public interface IDisplayCondition
{
    /// <summary>Stable identifier used for serialization.</summary>
    string Id { get; }

    /// <summary>Human readable name shown in the condition picker.</summary>
    string Name { get; }

    /// <summary>Evaluates this condition against the current game state.</summary>
    bool Check(ConditionArg arg);

    /// <summary>Short summary of the configured argument, shown in the set editor.</summary>
    string Describe(ConditionArg arg);

    /// <summary>
    /// Appends the argument editor controls to the given vertical container.
    /// </summary>
    /// <param name="container">Vertical container to append controls to.</param>
    /// <param name="arg">Argument bag to edit.</param>
    /// <param name="idSuffix">
    /// Unique suffix appended to control ids. The same condition type can appear
    /// several times in one set, and ImGui control ids must stay unique.
    /// </param>
    /// <param name="onChanged">Invoked whenever the argument changes.</param>
    void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged);
}

/// <summary>Groups conditions into a section of the condition picker.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ConditionCategoryAttribute(string name, int order = 0) : Attribute
{
    public string Name  { get; } = name;
    public int    Order { get; } = order;
}
