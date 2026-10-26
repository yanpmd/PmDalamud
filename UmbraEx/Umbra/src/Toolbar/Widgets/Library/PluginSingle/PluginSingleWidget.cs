using Dalamud.Interface;
using Dalamud.Plugin;

namespace Umbra.Widgets;

[ToolbarWidget(
    "PluginSingle",
    "Widget.PluginSingle.Name",
    "Widget.PluginSingle.Description",
    ["plugin", "addon", "dalamud", "single", "button", "menu"]
)]
internal sealed partial class PluginSingleWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    protected override string          DefaultIconType        => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.Plug;

    protected override void OnLoad()
    {
        Node.OnClick      += OnClick;
        Node.OnRightClick += OnRightClick;
    }

    protected override void OnUnload()
    {
        Node.OnClick      -= OnClick;
        Node.OnRightClick -= OnRightClick;
    }

    protected override void OnDraw()
    {
        IExposedPlugin? plugin = GetSelectedPlugin();

        if (plugin is null) {
            SetText("");
            SetDisabled(true);
            Node.Tooltip = default;

            return;
        }

        SetText(plugin.Name);
        SetDisabled(!plugin.IsLoaded);

        Node.Tooltip = GetConfigValue<bool>("ShowTooltip")
            ? I18N.Translate(
                plugin.IsLoaded
                    ? "Widget.PluginSingle.Tooltip"
                    : "Widget.PluginSingle.Tooltip.NotLoaded"
            )
            : null;
    }

    public override string GetInstanceName()
    {
        IExposedPlugin? plugin = GetSelectedPlugin();

        return plugin is null ? base.GetInstanceName() : $"{base.GetInstanceName()} - {plugin.Name}";
    }

    private IExposedPlugin? GetSelectedPlugin()
    {
        string internalName = GetConfigValue<string>("SelectedPlugin");

        if (internalName.Length == 0) return null;

        return Framework.DalamudPlugin.InstalledPlugins.FirstOrDefault(p => p.InternalName == internalName);
    }

    private void OnClick(Node _)
    {
        IExposedPlugin? plugin = GetSelectedPlugin();

        if (plugin is not { IsLoaded: true }) return;

        // Mirrors the behaviour of a single entry in the plugin list menu.
        if ((ImGui.GetIO().KeyShift || ImGui.GetIO().KeyCtrl) && plugin.HasConfigUi) {
            plugin.OpenConfigUi();

            return;
        }

        if (plugin.HasMainUi) {
            plugin.OpenMainUi();
        } else if (plugin.HasConfigUi) {
            plugin.OpenConfigUi();
        }
    }

    private void OnRightClick(Node _)
    {
        IExposedPlugin? plugin = GetSelectedPlugin();

        if (plugin is { IsLoaded: true, HasConfigUi: true }) {
            plugin.OpenConfigUi();
        }
    }
}
