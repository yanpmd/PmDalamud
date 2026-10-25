using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Umbra.QoLBar;

public interface IDisplayPriority
{
    public int DisplayPriority { get; }
}

public interface ICondition : IDisplayPriority
{
    public string ID { get; }

    /// <summary>
    /// Localized display name. Defaults to looking up the implementing type's
    /// name in the localization files, i.e. "CharacterCondition".
    /// </summary>
    public string ConditionName => Loc.Get(GetType().Name, GetType().Name);

    public bool Check(dynamic arg);
}

public interface IDrawableCondition
{
    public string GetTooltip(CndCfg cndCfg);
    public string GetSelectableTooltip(CndCfg cndCfg);
    public void Draw(CndCfg cndCfg);
}

public interface IArgCondition
{
    public dynamic GetDefaultArg(CndCfg cndCfg);
}

public interface IOnImportCondition
{
    public void OnImport(CndCfg cndCfg);
}

public interface IConditionCategory : IDisplayPriority
{
    /// <summary>
    /// Localized display name. Defaults to looking up the implementing type's
    /// name in the localization files.
    /// </summary>
    public string CategoryName => Loc.Get(GetType().Name, GetType().Name);
}

public interface IConditionSetPreset
{
    public string Name => Loc.Get(GetType().Name, GetType().Name);

    public CndSetCfg Generate();
}

/// <summary>
/// Evaluates condition sets. Ported from QoLBarCN's condition engine.
/// </summary>
public static class ConditionManager
{
    public enum BinaryOperator
    {
        AND,
        OR,
        EQUALS,
        XOR
    }

    private static readonly Stopwatch runTime = Stopwatch.StartNew();

    public static float RunTime => (float)runTime.Elapsed.TotalSeconds;

    private static readonly Dictionary<string, ICondition> conditions = new();
    private static readonly Dictionary<ICondition, IConditionCategory> categoryMap = new();
    private static readonly Dictionary<(ICondition, dynamic), bool> conditionCache = new();
    private static readonly Dictionary<CndSetCfg, (bool prev, float time)> conditionSetCache = new();
    private static readonly Dictionary<CndSetCfg, List<bool>> debugSteps = new();
    private static readonly HashSet<CndSetCfg> lockedSets = new();
    private static float lastConditionCache = 0;

    public static List<(IConditionCategory category, List<ICondition> conditions)> ConditionCategories { get; private set; } = new();
    public static List<IConditionSetPreset> Presets { get; } = new();

    public static void Initialize()
    {
        foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(IConditionCategory)) && !t.IsInterface))
        {
            var category = (IConditionCategory)Activator.CreateInstance(t);
            if (category == null) continue;

            var list = new List<ICondition>();
            ConditionCategories.Add((category, list));
            if (!t.IsAssignableTo(typeof(ICondition))) continue;

            list.Add((ICondition)category);
        }

        foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(ICondition)) && !t.IsInterface))
        {
            var condition = (ICondition)Activator.CreateInstance(t);
            if (condition == null) continue;

            conditions.Add(condition.ID, condition);

            var categoryType = t.GetCustomAttributes().FirstOrDefault(attr => attr.GetType().IsAssignableTo(typeof(IConditionCategory)))?.GetType();
            if (categoryType != null)
            {
                var (category, list) = ConditionCategories.FirstOrDefault(tuple => tuple.category.GetType() == categoryType);
                if (category != null)
                {
                    list.Add(condition);
                    categoryMap.Add(condition, category);
                    continue;
                }
            }

            if (t.IsAssignableTo(typeof(IConditionCategory)) && !categoryMap.ContainsKey(condition))
                categoryMap.Add(condition, (IConditionCategory)condition);
        }

        ConditionCategories = ConditionCategories.OrderBy(t => t.category.DisplayPriority).ToList();
        for (int i = 0; i < ConditionCategories.Count; i++)
        {
            var (category, list) = ConditionCategories[i];
            ConditionCategories[i] = (category, list.OrderBy(c => c.DisplayPriority).ToList());
        }

        foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(IConditionSetPreset)) && !t.IsInterface))
        {
            var preset = (IConditionSetPreset)Activator.CreateInstance(t);
            if (preset == null) continue;

            Presets.Add(preset);
        }
    }

    public static ICondition GetCondition(string id) => conditions.TryGetValue(id, out var condition) ? condition : null;

    public static IConditionCategory GetConditionCategory(ICondition condition) => categoryMap.TryGetValue(condition, out var category) ? category : null;

    public static IConditionCategory GetConditionCategory(string id) => GetConditionCategory(GetCondition(id));

    public static bool CheckCondition(string id, dynamic arg = null, bool negate = false)
    {
        var condition = GetCondition(id);
        return condition != null && (!negate ? CheckCondition(condition, arg) : !CheckCondition(condition, arg));
    }

    private static bool CheckCondition(ICondition condition, dynamic arg)
    {
        if (conditionCache.TryGetValue((condition, arg), out bool cache))
            return cache;

        try
        {
            cache = condition.Check(arg);
        }
        catch
        {
            cache = false;
        }

        conditionCache[(condition, arg)] = cache;
        return cache;
    }

    private static bool CheckUnaryCondition(bool negate, ICondition condition, dynamic arg)
    {
        try
        {
            return !negate ? condition.Check(arg) : !condition.Check(arg);
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckBinaryCondition(bool prev, BinaryOperator op, bool negate, ICondition condition, dynamic arg)
    {
        return op switch
        {
            BinaryOperator.AND    => prev && CheckUnaryCondition(negate, condition, arg),
            BinaryOperator.OR     => prev || CheckUnaryCondition(negate, condition, arg),
            BinaryOperator.EQUALS => prev == CheckUnaryCondition(negate, condition, arg),
            BinaryOperator.XOR    => prev ^ CheckUnaryCondition(negate, condition, arg),
            _                     => prev
        };
    }

    public static bool CheckConditionSet(int i) => i >= 0 && i < QoLBarManager.Instance.Config.ConditionSets.Count && CheckConditionSet(QoLBarManager.Instance.Config.ConditionSets[i]);

    public static bool CheckConditionSet(CndSetCfg set)
    {
        if (lockedSets.Contains(set))
            return conditionSetCache.TryGetValue(set, out var c) && c.prev;

        if (conditionSetCache.TryGetValue(set, out var cache) && RunTime <= cache.time + (QoLBarManager.Instance.Config.Settings.NoConditionCache ? 0 : 0.1f))
            return cache.prev;

        lockedSets.Add(set);

        var first = true;
        var prev = true;
        var steps = new List<bool>();
        foreach (var cnd in set.Conditions)
        {
            var condition = GetCondition(cnd.ID);
            if (condition == null) continue;

            if (first)
            {
                prev = CheckUnaryCondition(cnd.Negate, condition, cnd.Arg);
                first = false;
            }
            else
            {
                prev = CheckBinaryCondition(prev, cnd.Operator, cnd.Negate, condition, cnd.Arg);
            }

            steps.Add(prev);
        }

        lockedSets.Remove(set);

        conditionSetCache[set] = (prev, RunTime);
        debugSteps[set] = steps;
        return prev;
    }

    public static List<bool> GetDebugSteps(CndSetCfg set) => debugSteps.TryGetValue(set, out var steps) ? steps : null;

    public static void UpdateCache()
    {
        if (QoLBarManager.Instance.Config.Settings.NoConditionCache)
        {
            conditionCache.Clear();
            return;
        }

        if (RunTime < lastConditionCache + 0.1f) return;

        conditionCache.Clear();
        lastConditionCache = RunTime;
    }

    public static void SwapConditionSet(int from, int to)
    {
        var config = QoLBarManager.Instance.Config;
        var set = config.ConditionSets[from];

        foreach (var bar in config.Bars.Values)
        {
            if (bar.ConditionSet == from)
                bar.ConditionSet = to;
            else if (bar.ConditionSet == to)
                bar.ConditionSet = from;
        }

        foreach (var condition in from s in config.ConditionSets from condition in s.Conditions where condition.ID == Umbra.QoLBar.Conditions.ConditionSetCondition.ConstId select condition)
        {
            if (condition.Arg == from)
                condition.Arg = to;
            else if (condition.Arg == to)
                condition.Arg = from;
        }

        config.ConditionSets.RemoveAt(from);
        config.ConditionSets.Insert(to, set);
        QoLBarManager.Instance.SaveConfig();
    }

    public static void RemoveConditionSet(int i)
    {
        var config = QoLBarManager.Instance.Config;

        foreach (var bar in config.Bars.Values)
        {
            if (bar.ConditionSet > i)
                bar.ConditionSet -= 1;
            else if (bar.ConditionSet == i)
                bar.ConditionSet = -1;
        }

        foreach (var s in config.ConditionSets)
        {
            for (int j = s.Conditions.Count - 1; j >= 0; j--)
            {
                var cond = s.Conditions[j];
                if (cond.ID != Umbra.QoLBar.Conditions.ConditionSetCondition.ConstId) continue;

                if (cond.Arg > i)
                    cond.Arg -= 1;
                else if (cond.Arg == i)
                    s.Conditions.RemoveAt(j);
            }
        }

        config.ConditionSets.RemoveAt(i);
        QoLBarManager.Instance.SaveConfig();
    }

    public static void ShiftCondition(CndSetCfg set, CndCfg cndCfg, bool increment)
    {
        var i = set.Conditions.IndexOf(cndCfg);
        if (!increment ? i <= 0 : i >= (set.Conditions.Count - 1)) return;

        var j = (increment ? i + 1 : i - 1);
        var condition = set.Conditions[i];
        set.Conditions.RemoveAt(i);
        set.Conditions.Insert(j, condition);
        QoLBarManager.Instance.SaveConfig();
    }
}
