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

            new BooleanWidgetConfigVariable(
                "ShowTooltip",
                I18N.Translate("Widget.PluginSingle.Config.ShowTooltip.Name"),
                I18N.Translate("Widget.PluginSingle.Config.ShowTooltip.Description"),
                true
            ),
        ];
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
