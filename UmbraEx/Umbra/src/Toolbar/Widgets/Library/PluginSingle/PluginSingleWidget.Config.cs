using Dalamud.Plugin;

namespace Umbra.Widgets;

internal sealed partial class PluginSingleWidget
{
    /// <inheritdoc/>
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            ..base.GetConfigVariables(),

            new SelectWidgetConfigVariable(
                "SelectedPlugin",
                I18N.Translate("Widget.PluginSingle.Config.SelectedPlugin.Name"),
                I18N.Translate("Widget.PluginSingle.Config.SelectedPlugin.Description"),
                "",
                GetPluginOptions()
            ),

            new StringWidgetConfigVariable(
                "ButtonLabel",
                I18N.Translate("Widget.PluginSingle.Config.ButtonLabel.Name"),
                I18N.Translate("Widget.PluginSingle.Config.ButtonLabel.Description"),
                "",
                1024,
                true
            ),

            new BooleanWidgetConfigVariable(
                "ShowTooltip",
                I18N.Translate("Widget.PluginSingle.Config.ShowTooltip.Name"),
                I18N.Translate("Widget.PluginSingle.Config.ShowTooltip.Description"),
                true
            ),
        ];
    }

    /// <summary>
    /// Returns the user-provided label of this widget, or an empty string when
    /// the name of the selected plugin should be used instead.
    /// </summary>
    private string GetButtonLabel()
    {
        return GetConfigValue<string>("ButtonLabel").Trim();
    }

    private static Dictionary<string, string> GetPluginOptions()
    {
        Dictionary<string, string> options = new() {
            [""] = I18N.Translate("Widget.PluginSingle.Config.SelectedPlugin.None"),
        };

        foreach (IExposedPlugin plugin in Framework.DalamudPlugin.InstalledPlugins.OrderBy(p => p.Name)) {
            options.TryAdd(plugin.InternalName, plugin.Name);
        }

        return options;
    }
}
