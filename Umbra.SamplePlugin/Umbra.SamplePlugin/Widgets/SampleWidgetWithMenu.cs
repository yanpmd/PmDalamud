using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Umbra.Common;
using Umbra.Widgets;

namespace Umbra.SamplePlugin.Widgets;

/// <summary>
/// A toolbar widget that contains a little menu.
///
/// Refer to the <see cref="SampleWidget"/> class for a simpler example
/// that contains more detailed explanations.
/// </summary>
[ToolbarWidget(
    "SampleWidgetWithAMenu",
    "A Sample Widget with a menu",
    "This is a sample widget from the Umbra.SamplePlugin repository that has a menu."
)]
public class SampleWidgetWithMenu(
    WidgetInfo info,
    string? guid = null,
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

    /// <inheritdoc/>
    public override MenuPopup Popup { get; } = new();

    // Services can be fetched from the framework like so.
    // Since toolbar widgets are always instantiated after the framework is
    // initialized, it's safe to fetch services in the constructor.
    private IToastGui ToastGui { get; set; } = Framework.Service<IToastGui>();

    /// <inheritdoc/>
    protected override void OnLoad()
    {
        // Let's add some buttons.
        Popup.Add(new MenuPopup.Button("MyButton") {
            OnClick = () => OnItemClicked("Button 1"),
            Icon = 14u,
            AltText = "Alt-Text here"
        });

        // You can also add groups...
        var group = new MenuPopup.Group("A button group");
        
        group.Add(new MenuPopup.Button("MyButton2") {
            OnClick = () => OnItemClicked("Button 2"),
            Icon = 14u,
            AltText = "Alt-Text here"
        });
        
        group.Add(new MenuPopup.Button("MyButton3") {
            OnClick = () => OnItemClicked("Button 3"),
            Icon = 14u,
            AltText = "Alt-Text here"
        });
        
        Popup.Add(group);
    }

    /// <inheritdoc/>
    protected override void OnDraw()
    {
        // Labels can be updated during runtime as well.
        SetText("A sample menu");
    }

    private void OnItemClicked(string id)
    {
        // Pull a service from the framework.
        ToastGui.ShowNormal($"You clicked button [{id}]");
    }
}
