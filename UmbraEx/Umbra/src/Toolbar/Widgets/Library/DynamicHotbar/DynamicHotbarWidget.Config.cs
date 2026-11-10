namespace Umbra.Widgets.Library.DynamicHotbar;

internal sealed partial class DynamicHotbarWidget
{
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            ..base.GetConfigVariables(),

            new IntegerWidgetConfigVariable(
                CvarNameNumSlots,
                I18N.Translate("Widget.DynamicHotbar.Config.NumSlots.Name"),
                I18N.Translate("Widget.DynamicHotbar.Config.NumSlots.Description"),
                8,
                4,
                20
            ),

            // The slot data is a compressed blob that is far too long for a regular config variable, so it
            // is hidden from the configuration interface and stored as-is.
            new StringWidgetConfigVariable(CvarNameSlotData, "", null, "", 0) { IsHidden = true },
        ];
    }
}
