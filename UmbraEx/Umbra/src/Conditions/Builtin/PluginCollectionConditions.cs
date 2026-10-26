using Umbra.Widgets.Library.PluginCollections;

namespace Umbra.Conditions.Builtin;

/// <summary>
/// True when the selected Dalamud plugin collection ("profile") is enabled.
/// </summary>
[ConditionCategory("Plugins", 40)]
internal sealed class PluginCollectionEnabledCondition : IDisplayCondition
{
    /// <summary>
    /// Reading the collections goes through reflection, so the result is cached
    /// briefly instead of being resolved on every frame.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMilliseconds(500);

    private static IReadOnlyList<DalamudProfile> _profiles   = [];
    private static DateTime                      _lastUpdate = DateTime.MinValue;

    public string Id   => "plugin-collection";
    public string Name => "插件合集已启用";

    public bool Check(ConditionArg arg)
    {
        if (string.IsNullOrWhiteSpace(arg.Text)) return false;

        foreach (DalamudProfile profile in GetProfiles()) {
            if (profile.Name == arg.Text) return profile.IsEnabled;
        }

        return false;
    }

    public string Describe(ConditionArg arg)
    {
        if (string.IsNullOrWhiteSpace(arg.Text)) return "（未选择）";

        foreach (DalamudProfile profile in GetProfiles()) {
            if (profile.Name != arg.Text) continue;

            return profile.IsEnabled ? $"{profile.Name}（已启用）" : $"{profile.Name}（未启用）";
        }

        return $"{arg.Text}（不存在）";
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        List<(string, string)> options = [("", "（未选择）")];

        options.AddRange(
            RefreshProfiles()
               .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
               .Select(profile => (profile.Name, profile.Name))
        );

        container.AppendChild(
            SelectHelper.Create(
                $"collection{idSuffix}",
                options,
                arg.Text,
                value => {
                    arg.Text = value;
                    onChanged();
                }
            )
        );
    }

    private static IReadOnlyList<DalamudProfile> GetProfiles()
    {
        return DateTime.UtcNow - _lastUpdate > CacheDuration ? RefreshProfiles() : _profiles;
    }

    private static IReadOnlyList<DalamudProfile> RefreshProfiles()
    {
        _lastUpdate = DateTime.UtcNow;
        _profiles   = DalamudProfileProvider.GetProfiles();

        return _profiles;
    }
}
