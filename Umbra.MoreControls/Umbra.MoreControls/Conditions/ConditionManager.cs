using System.Reflection;

namespace Umbra.MoreControls.Conditions;

internal sealed class ConditionCategoryGroup
{
    public string            Name  { get; init; } = "其他";
    public int               Order { get; init; }
    public List<IDisplayCondition>  Items { get; } = [];
}

/// <summary>
/// Registry and evaluation engine for display conditions.
///
/// The expression model is intentionally the same as QoLBar's: a flat list of
/// conditions folded left-to-right with AND/OR/XOR, each entry individually
/// negatable. Real grouping is achieved by referencing another condition set.
/// </summary>
internal static class ConditionManager
{
    private static readonly Dictionary<string, IDisplayCondition> Registry = [];
    private static readonly List<ConditionCategoryGroup>   Groups   = [];

    /// <summary>Guards against self-referencing condition sets.</summary>
    private static readonly HashSet<string> Evaluating = [];

    public static IReadOnlyList<ConditionCategoryGroup> Categories => Groups;

    public static void Initialize()
    {
        Registry.Clear();
        Groups.Clear();

        var discovered = new List<(IDisplayCondition Cond, string Category, int Order)>();

        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()) {
            if (type is not { IsClass: true, IsAbstract: false }) continue;
            if (!typeof(IDisplayCondition).IsAssignableFrom(type)) continue;

            try {
                if (Activator.CreateInstance(type) is not IDisplayCondition cond) continue;

                ConditionCategoryAttribute? attr = type.GetCustomAttribute<ConditionCategoryAttribute>();

                discovered.Add((cond, attr?.Name ?? "其他", attr?.Order ?? 100));
            } catch (Exception e) {
                Logger.Warning($"[MoreControls] Failed to create condition {type.Name}: {e.Message}");
            }
        }

        foreach ((IDisplayCondition cond, string category, int order) in discovered.OrderBy(e => e.Order)) {
            Registry[cond.Id] = cond;

            ConditionCategoryGroup? group = Groups.FirstOrDefault(g => g.Name == category);

            if (group is null) {
                group = new ConditionCategoryGroup { Name = category, Order = order };
                Groups.Add(group);
            }

            group.Items.Add(cond);
        }
    }

    public static IDisplayCondition? Get(string? id)
    {
        return id != null && Registry.TryGetValue(id, out IDisplayCondition? cond) ? cond : null;
    }

    public static IDisplayCondition? GetDefault()
    {
        return Registry.Values.FirstOrDefault();
    }

    /// <summary>Evaluates a single condition, swallowing exceptions as "false".</summary>
    public static bool CheckOne(ConditionConfig config)
    {
        IDisplayCondition? cond = Get(config.Id);

        if (cond is null) return false;

        bool value;

        try {
            value = cond.Check(config.Arg);
        } catch (Exception e) {
            Logger.Warning($"[MoreControls] Condition '{config.Id}' threw: {e.Message}");
            value = false;
        }

        return config.Negate ? !value : value;
    }

    public static bool EvaluateSetById(string? setId)
    {
        return EvaluateSet(ConfigStore.Config.FindSet(setId));
    }

    /// <summary>
    /// Evaluates a condition set. An empty/unbound set always evaluates to true
    /// so that targets without conditions stay visible.
    /// </summary>
    public static bool EvaluateSet(ConditionSetConfig? set)
    {
        if (set is null || set.Conditions.Count == 0) return true;
        if (!Evaluating.Add(set.Id)) return false;

        try {
            bool result = true;

            for (var i = 0; i < set.Conditions.Count; i++) {
                ConditionConfig cnd = set.Conditions[i];
                bool            val = CheckOne(cnd);

                if (i == 0) {
                    result = val;
                    continue;
                }

                result = cnd.Join switch {
                    ConditionJoin.Or  => result || val,
                    ConditionJoin.Xor => result ^ val,
                    _                 => result && val,
                };
            }

            return result;
        } finally {
            Evaluating.Remove(set.Id);
        }
    }

    /// <summary>
    /// Evaluates a target binding. Returns true when the target has no binding
    /// (i.e. it should be visible because no condition was configured).
    /// </summary>
    public static bool EvaluateBinding(ConditionBinding? binding)
    {
        if (binding is null || !binding.IsBound) return true;

        ConditionSetConfig? set = ConfigStore.Config.FindSet(binding.SetId);

        if (set is null) return true;

        bool result = EvaluateSet(set);

        return binding.Invert ? !result : result;
    }
}
