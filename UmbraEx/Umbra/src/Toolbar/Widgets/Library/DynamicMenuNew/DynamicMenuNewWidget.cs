using System.Reflection;

using Umbra.Widgets.System;

namespace Umbra.Widgets.Library.DynamicMenuNew;

[ToolbarWidget(
    "DynamicMenuNew",
    "Widget.DynamicMenuNew.Name",
    "Widget.DynamicMenuNew.Description",
    ["menu", "widgets", "container", "list", "group", "dynamic", "shortcut"]
)]
internal sealed partial class DynamicMenuNewWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    public override WidgetMenuPopup Popup { get; } = new();

    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    protected override string          DefaultIconType        => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.Bars;

    public override string GetInstanceName()
    {
        return $"{base.GetInstanceName()} - {GetConfigValue<string>("ButtonLabel")}";
    }

    protected override void OnLoad()
    {
        Popup.WidgetRef = this;

        OnConfigValueChanged += OnWidgetConfigValueChanged;

        LoadItems();
        RebuildChildren();
    }

    protected override void OnUnload()
    {
        OnConfigValueChanged -= OnWidgetConfigValueChanged;

        DisposeChildren();
    }

    protected override void OnDraw()
    {
        SetText(GetConfigValue<string>("ButtonLabel"));

        string tooltip = GetConfigValue<string>("ButtonTooltip").Trim();
        Node.Tooltip = string.IsNullOrEmpty(tooltip) ? null : tooltip;

        ProcessPendingActivation();
    }

    protected override void OnConfigurationChanged()
    {
        Popup.EntryHeight   = GetConfigValue<int>("MenuEntryHeight");
        Popup.EntryFontSize = GetConfigValue<int>("MenuFontSize");
    }

    private void OnWidgetConfigValueChanged(IWidgetConfigVariable variable)
    {
        if (variable.Id == "Items") {
            SyncItems();
        }
    }

    /// <summary>
    /// Opens the popup of the widget that was picked in the menu, or invokes
    /// its primary action when it doesn't have a popup of its own. This is
    /// deferred until after the menu popup has been closed.
    /// </summary>
    private void ProcessPendingActivation()
    {
        if (Popup.PendingActivation is not { } child) return;

        Popup.PendingActivation = null;

        if (child.Popup is { } childPopup) {
            Framework.Service<WidgetManager>().OpenHostedPopup(child, childPopup, this);

            return;
        }

        // Widgets without a popup act on plain mouse events, so replay those
        // instead of trying to emulate a real ImGui interaction.
        InvokeNodeMouseEvent(child.Node, "OnMouseDown");
        InvokeNodeMouseEvent(child.Node, "OnMouseUp");
        InvokeNodeMouseEvent(child.Node, "OnClick");
    }

    private static void InvokeNodeMouseEvent(Node node, string eventName)
    {
        if (node.IsDisabled) return;

        FieldInfo? field = typeof(Node).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (field?.GetValue(node) is not Delegate handler) return;

        try {
            handler.DynamicInvoke(node);
        } catch (Exception e) {
            Logger.Warning($"Failed to invoke \"{eventName}\" on a menu widget: {e.Message}");
        }
    }
}
