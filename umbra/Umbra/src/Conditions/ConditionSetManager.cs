using Newtonsoft.Json;

namespace Umbra.Conditions;

/// <summary>
/// Owns all condition sets: persistence, CRUD and evaluation.
///
/// Sets are stored as a single JSON cvar. Evaluation results are memoized for a
/// very short period so that the per-frame cost of N widgets is one evaluation
/// per set, not one per widget.
/// </summary>
[Service]
internal sealed class ConditionSetManager : IDisposable
{
    private const string CvarName = "Conditions.Data";

    /// <summary>How long a result may be reused, in milliseconds.</summary>
    private const long CacheTtl = 100;

    [ConfigVariable(CvarName)]
    private static string Data { get; set; } = "[]";

    private string _lastSerialized = string.Empty;

    private readonly ConditionRegistry                        _registry;
    private readonly List<ConditionSetConfig>                 _sets  = [];
    private readonly Dictionary<string, (bool Value, long At)> _cache = [];

    /// <summary>Guards against self-referencing condition sets.</summary>
    private readonly HashSet<string> _evaluating = [];

    public event Action<ConditionSetConfig>? SetCreated;
    public event Action<ConditionSetConfig>? SetDeleted;

    private static ConditionSetManager? _instance;

    /// <summary>
    /// Global accessor used from hot paths (every widget, every tick).
    /// </summary>
    public static ConditionSetManager Instance => _instance ??= Framework.Service<ConditionSetManager>();

    public ConditionSetManager(ConditionRegistry registry)
    {
        _registry = registry;
        _instance = this;

        Load();
        ConfigManager.CvarChanged += OnCvarChanged;
    }

    public void Dispose()
    {
        ConfigManager.CvarChanged -= OnCvarChanged;
    }

    /// <summary>Snapshot of all sets, in display order.</summary>
    public List<ConditionSetConfig> All => [.. _sets];

    public ConditionSetConfig? Get(string? id)
    {
        return string.IsNullOrEmpty(id) ? null : _sets.FirstOrDefault(set => set.Id == id);
    }

    public ConditionSetConfig CreateSet(string? name = null)
    {
        var set = new ConditionSetConfig {
            Name = string.IsNullOrWhiteSpace(name) ? $"Condition Set {_sets.Count + 1}" : name,
            // Start with the most common condition so a new set is immediately usable.
            Conditions = [
                new ConditionConfig {
                    Id     = "cf",
                    Negate = true,
                    Arg    = new ConditionArg { Text = "InCombat" },
                },
            ],
        };

        _sets.Add(set);
        Persist();
        SetCreated?.Invoke(set);

        return set;
    }

    public void DeleteSet(string id)
    {
        ConditionSetConfig? set = Get(id);

        if (set is null) return;

        _sets.Remove(set);
        _cache.Remove(id);
        Persist();
        SetDeleted?.Invoke(set);
    }

    public void Persist()
    {
        _cache.Clear();

        // Remember what we wrote so that the cvar-changed handler can tell our
        // own writes apart from an external reload.
        _lastSerialized = JsonConvert.SerializeObject(_sets);

        try {
            ConfigManager.Set(CvarName, _lastSerialized);
        } catch (Exception e) {
            // The cvar may not be registered yet; never let saving crash the UI.
            Logger.Warning($"Failed to save condition sets: {e.Message}");
        }
    }

    /// <summary>
    /// Cache-free, exception-safe visibility check for the hot path (every
    /// widget, every tick). Falls back to "visible" when anything goes wrong.
    /// </summary>
    public static bool IsMatchSafe(string? setId)
    {
        if (string.IsNullOrEmpty(setId)) return true;

        try {
            return Instance.IsMatch(setId);
        } catch (Exception e) {
            Logger.Warning($"Failed to evaluate condition set '{setId}': {e.Message}");

            return true;
        }
    }

    /// <summary>
    /// True when the target may be shown. Targets without a set (or pointing at
    /// a set that no longer exists) are always visible.
    /// </summary>
    public bool IsMatch(string? setId, bool ignoreCache = false)
    {
        if (string.IsNullOrEmpty(setId)) return true;

        ConditionSetConfig? set = Get(setId);

        if (set is null || set.Conditions.Count == 0) return true;

        long now = Environment.TickCount64;

        if (!ignoreCache && _cache.TryGetValue(setId, out (bool Value, long At) entry) && now - entry.At < CacheTtl) {
            return entry.Value;
        }

        bool result = Evaluate(setId);

        _cache[setId] = (result, now);

        return result;
    }

    /// <summary>Evaluates a set without using the cache. Used by nested set references.</summary>
    public bool Evaluate(string? setId)
    {
        ConditionSetConfig? set = Get(setId);

        if (set is null || set.Conditions.Count == 0) return true;
        if (!_evaluating.Add(set.Id)) return false;

        try {
            bool result = true;

            for (var i = 0; i < set.Conditions.Count; i++) {
                ConditionConfig condition = set.Conditions[i];
                bool            value     = _registry.Check(condition);

                if (i == 0) {
                    result = value;

                    continue;
                }

                result = condition.Join switch {
                    ConditionJoin.Or  => result || value,
                    ConditionJoin.Xor => result ^ value,
                    _                 => result && value,
                };
            }

            return result;
        } finally {
            _evaluating.Remove(set.Id);
        }
    }

    /// <summary>Choices suitable for a select control: id -> label.</summary>
    public Dictionary<string, string> GetChoices(bool includeNone = true)
    {
        var choices = new Dictionary<string, string>();

        if (includeNone) choices[string.Empty] = I18N.Translate("Widget.ConditionSet.None");

        foreach (ConditionSetConfig set in _sets) {
            choices[set.Id] = string.IsNullOrWhiteSpace(set.Name) ? "(unnamed)" : set.Name;
        }

        return choices;
    }

    private void Load()
    {
        List<ConditionSetConfig>? sets = Deserialize(Data);

        _lastSerialized = Data;
        _sets.Clear();

        if (sets is not null) _sets.AddRange(sets);
    }

    private void OnCvarChanged(string name)
    {
        if (name != CvarName) return;

        // Ignore our own writes; re-merging them would invalidate editor references.
        if (Data == _lastSerialized) return;

        List<ConditionSetConfig>? sets = Deserialize(Data);

        if (sets is null) return;

        _lastSerialized = Data;

        // Merge in place so that editor UI holding references stays valid.
        foreach (ConditionSetConfig incoming in sets) {
            ConditionSetConfig? existing = _sets.FirstOrDefault(set => set.Id == incoming.Id);

            if (existing is null) {
                _sets.Add(incoming);
            } else if (!ReferenceEquals(existing, incoming)) {
                existing.Update(incoming);
            }
        }

        foreach (ConditionSetConfig existing in _sets.ToList()) {
            if (sets.All(set => set.Id != existing.Id)) _sets.Remove(existing);
        }

        _cache.Clear();
    }

    private static List<ConditionSetConfig>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try {
            List<ConditionSetConfig>? sets = JsonConvert.DeserializeObject<List<ConditionSetConfig>>(json);

            if (sets is null) return null;

            foreach (ConditionSetConfig set in sets) {
                set.Conditions ??= [];

                foreach (ConditionConfig condition in set.Conditions) {
                    condition.Arg ??= new();
                }
            }

            return sets;
        } catch (Exception e) {
            Logger.Error($"Failed to read condition sets: {e.Message}");

            return null;
        }
    }
}
