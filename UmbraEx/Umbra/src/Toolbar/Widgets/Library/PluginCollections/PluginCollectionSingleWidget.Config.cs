namespace Umbra.Widgets.Library.PluginCollections;

internal sealed partial class PluginCollectionSingleWidget
{
    /// <summary>
    /// Names of the collections the configuration variables were built with.
    /// </summary>
    private List<string> _configProfiles = [];

    /// <inheritdoc/>
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            ..base.GetConfigVariables(),

            new SelectWidgetConfigVariable(
                "SelectedCollection",
                I18N.Translate("Widget.PluginCollectionSingle.Config.SelectedCollection.Name"),
                I18N.Translate("Widget.PluginCollectionSingle.Config.SelectedCollection.Description"),
                "",
                GetCollectionOptions()
            ),

            new StringWidgetConfigVariable(
                "ButtonLabel",
                I18N.Translate("Widget.PluginCollectionSingle.Config.ButtonLabel.Name"),
                I18N.Translate("Widget.PluginCollectionSingle.Config.ButtonLabel.Description"),
                "",
                1024,
                true
            ),

            new BooleanWidgetConfigVariable(
                "ShowTooltip",
                I18N.Translate("Widget.PluginCollectionSingle.Config.ShowTooltip.Name"),
                I18N.Translate("Widget.PluginCollectionSingle.Config.ShowTooltip.Description"),
                true
            ) { Category = I18N.Translate("Widget.ConfigCategory.MenuAppearance") },
        ];
    }

    private Dictionary<string, string> GetCollectionOptions()
    {
        Dictionary<string, string> options = new() {
            [""] = I18N.Translate("Widget.PluginCollectionSingle.Config.SelectedCollection.None"),
        };

        List<string> names = [];

        foreach (DalamudProfile profile in DalamudProfileProvider.GetProfiles()) {
            if (string.IsNullOrWhiteSpace(profile.Name)) continue;

            names.Add(profile.Name);
            options.TryAdd(profile.Name, profile.Name);
        }

        _configProfiles = names;

        return options;
    }

    /// <summary>
    /// Whether the given profiles differ from the ones the configuration
    /// variables were built with. If so, the options need to be rebuilt so that
    /// newly created or removed collections show up without a restart.
    /// </summary>
    private bool HasProfileListChanged(IReadOnlyList<DalamudProfile> profiles)
    {
        if (profiles.Count != _configProfiles.Count) return true;

        foreach (DalamudProfile profile in profiles) {
            if (!_configProfiles.Contains(profile.Name)) return true;
        }

        return false;
    }
}
