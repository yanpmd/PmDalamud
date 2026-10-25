namespace Umbra.Widgets.Library.PluginCollections;

internal sealed partial class PluginCollectionsWidget
{
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            ..base.GetConfigVariables(),
            new BooleanWidgetConfigVariable(
                "ShowLabel",
                I18N.Translate("Widget.PluginCollections.Config.ShowLabel.Name"),
                I18N.Translate("Widget.PluginCollections.Config.ShowLabel.Description"),
                true
            ),
            new BooleanWidgetConfigVariable(
                "ShowDisabledCollections",
                I18N.Translate("Widget.PluginCollections.Config.ShowDisabled.Name"),
                I18N.Translate("Widget.PluginCollections.Config.ShowDisabled.Description"),
                true
            ) { Category = I18N.Translate("Widget.ConfigCategory.MenuAppearance") },
            new BooleanWidgetConfigVariable(
                "ShowTooltip",
                I18N.Translate("Widget.PluginCollections.Config.ShowTooltip.Name"),
                I18N.Translate("Widget.PluginCollections.Config.ShowTooltip.Description"),
                true
            ) { Category = I18N.Translate("Widget.ConfigCategory.MenuAppearance") },
        ];
    }
}
