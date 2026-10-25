using Umbra.Windows.Components;

namespace Umbra.Conditions.Builtin;

/// <summary>
/// Umbra's <see cref="SelectNode"/> stores the visible label as its value, so
/// conditions keep a label -> id map next to the node.
/// </summary>
internal static class SelectHelper
{
    public static SelectNode Create(
        string                     id,
        IEnumerable<(string Id, string Label)> options,
        string?                    currentId,
        Action<string>             onChanged,
        string?                    label       = null,
        string?                    description = null
    )
    {
        List<(string Id, string Label)> list = options.ToList();

        var labels = list.Select(entry => entry.Label).Distinct().ToList();
        var map    = list.GroupBy(entry => entry.Label).ToDictionary(group => group.Key, group => group.First().Id);

        string current = list.FirstOrDefault(entry => entry.Id == currentId).Label
            ?? labels.FirstOrDefault()
            ?? string.Empty;

        var node = new SelectNode(id, current, labels, label, description, 0);

        node.OnValueChanged += value => {
            if (map.TryGetValue(value, out string? mapped)) onChanged(mapped);
        };

        return node;
    }
}
