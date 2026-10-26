using System.Reflection;

namespace Umbra.Conditions;

public sealed class ConditionCategoryGroup
{
    public string                   Name  { get; init; } = string.Empty;
    public int                      Order { get; init; }
    public List<IDisplayCondition>  Items { get; }      = [];
}

/// <summary>
/// Discovers and owns all <see cref="IDisplayCondition"/> implementations in
/// this assembly.
/// </summary>
[Service]
internal sealed class ConditionRegistry
{
    private readonly Dictionary<string, IDisplayCondition> _conditions = [];
    private readonly List<ConditionCategoryGroup>          _groups     = [];

    public ConditionRegistry()
    {
        Discover();
    }

    public IReadOnlyList<ConditionCategoryGroup> Categories => _groups;

    public IDisplayCondition? Default => _conditions.Values.FirstOrDefault();

    public IDisplayCondition? Get(string? id)
    {
        return id is not null && _conditions.TryGetValue(id, out IDisplayCondition? condition) ? condition : null;
    }

    /// <summary>Evaluates one condition, including its negation flag.</summary>
    public bool Check(ConditionConfig config)
    {
        IDisplayCondition? condition = Get(config.Id);

        if (condition is null) return false;

        bool value;

        try {
            value = condition.Check(config.Arg);
        } catch (Exception e) {
            Logger.Warning($"Condition '{config.Id}' threw: {e.Message}");
            value = false;
        }

        return config.Negate ? !value : value;
    }

    private void Discover()
    {
        var discovered = new List<(IDisplayCondition Condition, string Category, int Order)>();

        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()) {
            if (type is not { IsClass: true, IsAbstract: false }) continue;
            if (!typeof(IDisplayCondition).IsAssignableFrom(type)) continue;

            try {
                if (Activator.CreateInstance(type) is not IDisplayCondition condition) continue;

                ConditionCategoryAttribute? attribute = type.GetCustomAttribute<ConditionCategoryAttribute>();

                discovered.Add((condition, attribute?.Name ?? "Other", attribute?.Order ?? 100));
            } catch (Exception e) {
                Logger.Warning($"Failed to create condition '{type.Name}': {e.Message}");
            }
        }

        foreach ((IDisplayCondition condition, string category, int order) in discovered.OrderBy(entry => entry.Order)) {
            _conditions[condition.Id] = condition;

            ConditionCategoryGroup? group = _groups.FirstOrDefault(entry => entry.Name == category);

            if (group is null) {
                group = new ConditionCategoryGroup { Name = category, Order = order };
                _groups.Add(group);
            }

            group.Items.Add(condition);
        }

        Logger.Info($"Registered {_conditions.Count} display conditions in {_groups.Count} categories.");
    }
}
