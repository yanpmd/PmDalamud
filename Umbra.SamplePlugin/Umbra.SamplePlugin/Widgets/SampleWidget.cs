using System.Collections.Generic;
using Umbra.Widgets;

namespace Umbra.SamplePlugin.Widgets;

[ToolbarWidget(
    // A unique ID for this type of widget. Don't change this once it's set,
    // because it is used to associate saved settings with this widget type.
    "SampleWidget",

    // The display name of this widget.
    "A Sample Widget",

    // A brief description of this widget that the user can see in the
    // "Add Widget" window.
    "This is a sample widget from the Umbra.SamplePlugin repository."
)]
public class SampleWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    /// <summary>
    /// Define which features of the "StandardToolbarWidget" this widget
    /// should be using.
    /// </summary>
    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    /// <summary>
    /// Defines the popup of this widget. Setting a value will make the widget
    /// interactive and will render the popup node when the widget is clicked.
    /// </summary>
    public override WidgetPopup? Popup { get; } = null;

    /// <summary>
    /// Returns a list of configuration variables that can be set by the user
    /// for instances of this widget.
    /// </summary>
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            // When extending from StandardToolbarWidget, always make sure to
            // also add the base variables.
            ..base.GetConfigVariables(),
        ];
    }

    /// <summary>
    /// This method is invoked once an instance of this widget has been added
    /// to the toolbar. You can use this method to pull in any data or perform
    /// one-time operations that are required for the widget to function.
    /// </summary>
    protected override void OnLoad()
    {
        SetText("A sample widget");
        SetGameIconId(14);
    }

    /// <summary>
    /// This method is invoked on every tick of the game loop. You can use this
    /// method to update the state of the widget based on the current game state
    /// or user configuration.
    /// </summary>
    protected override void OnDraw()
    {
    }

    /// <summary>
    /// Invoked when the widget is removed from the toolbar or disposed of due
    /// to other reasons such as config profile changes, Umbra unloading, etc.
    /// </summary>
    protected override void OnUnload()
    {
    }
}
