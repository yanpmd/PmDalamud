namespace Umbra.Widgets.Library.DynamicMenuNew;

internal sealed partial class DynamicMenuNewWidget
{
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            ..base.GetConfigVariables(),

            new StringWidgetConfigVariable(
                "ButtonLabel",
                I18N.Translate("Widget.DynamicMenuNew.Config.ButtonLabel.Name"),
                I18N.Translate("Widget.DynamicMenuNew.Config.ButtonLabel.Description"),
                "Widget Menu",
                1024,
                true
            ),
            new StringWidgetConfigVariable(
                "ButtonTooltip",
                I18N.Translate("Widget.DynamicMenuNew.Config.ButtonTooltip.Name"),
                I18N.Translate("Widget.DynamicMenuNew.Config.ButtonTooltip.Description"),
                "",
                1024,
                true
            ),
            new IntegerWidgetConfigVariable(
                "MenuEntryHeight",
                I18N.Translate("Widget.DynamicMenuNew.Config.MenuEntryHeight.Name"),
                I18N.Translate("Widget.DynamicMenuNew.Config.MenuEntryHeight.Description"),
                32,
                20,
                64
            ) { Category = I18N.Translate("Widget.ConfigCategory.MenuAppearance") },
            new IntegerWidgetConfigVariable(
                "MenuFontSize",
                I18N.Translate("Widget.DynamicMenuNew.Config.MenuFontSize.Name"),
                I18N.Translate("Widget.DynamicMenuNew.Config.MenuFontSize.Description"),
                13,
                10,
                20
            ) { Category = I18N.Translate("Widget.ConfigCategory.MenuAppearance") },
            new StringWidgetConfigVariable("Items", "", null, "", short.MaxValue) { IsHidden = true },
        ];
    }

    /// <summary>
    /// Opens the window in which the widgets that are shown in this menu are
    /// managed. The button is added to the footer of the settings window.
    /// </summary>
    public override Node? CreateSettingsExtraButton()
    {
        ButtonNode button = new(
            "DynamicMenuNewManage",
            I18N.Translate("Widget.DynamicMenuNew.Config.Manage.Name"),
            FontAwesomeIcon.List
        );

        button.OnClick += _ => OpenContentEditor();

        return button;
    }
}
