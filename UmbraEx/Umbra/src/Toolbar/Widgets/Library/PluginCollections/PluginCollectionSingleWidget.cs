using Dalamud.Interface;

namespace Umbra.Widgets.Library.PluginCollections;

[ToolbarWidget(
    "PluginCollectionSingle",
    "Widget.PluginCollectionSingle.Name",
    "Widget.PluginCollectionSingle.Description",
    ["plugin", "collection", "profile", "dalamud", "group", "toggle", "single"]
)]
internal sealed partial class PluginCollectionSingleWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    private IReadOnlyList<DalamudProfile> _profiles   = [];
    private DateTime                      _lastUpdate = DateTime.MinValue;

    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    protected override string          DefaultIconType        => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.ToggleOn;

    protected override void OnLoad()
    {
        Node.OnClick      += OnClick;
        Node.OnRightClick += _ => OpenCollectionsManager();
    }

    protected override void OnUnload()
    {
        Node.OnClick -= OnClick;
    }

    protected override void OnDraw()
    {
        if (DateTime.UtcNow - _lastUpdate > TimeSpan.FromMilliseconds(500)) {
            RefreshProfiles();

            // Keep the collection picker in sync with the collections that
            // currently exist in Dalamud.
            if (HasProfileListChanged(_profiles)) {
                UpdateConfigVariables();
            }
        }

        bool showTooltip = GetConfigValue<bool>("ShowTooltip");
        string name      = GetConfigValue<string>("SelectedCollection");

        if (name.Length == 0) {
            SetText("");
            SetDisabled(true);
            Node.Tooltip = default;

            return;
        }

        DalamudProfile? profile = FindProfile(name);

        if (profile is null) {
            SetText(GetButtonLabel(name));
            SetDisabled(true);
            SetTextColor(new("Widget.TextMuted"), null);
            Node.Tooltip = showTooltip ? I18N.Translate("Widget.PluginCollectionSingle.Unavailable") : null;

            return;
        }

        DalamudProfile selected = profile.Value;

        SetDisabled(false);
        SetText(GetButtonLabel(selected.Name));
        SetIconDesaturated(CvarDesaturateIcon() || !selected.IsEnabled);

        if (!selected.IsEnabled) {
            SetTextColor(new("Widget.TextMuted"), null);

            // The default icon doubles as a state indicator so that disabled
            // collections can be told apart at a glance.
            if (IsUsingDefaultIcon()) SetFontAwesomeIcon(FontAwesomeIcon.ToggleOff);
        }

        Node.Tooltip = showTooltip
            ? I18N.Translate(
                selected.IsEnabled
                    ? "Widget.PluginCollectionSingle.Tooltip.Enabled"
                    : "Widget.PluginCollectionSingle.Tooltip.Disabled"
            )
            : null;
    }

    public override string GetInstanceName()
    {
        string name = GetConfigValue<string>("SelectedCollection");

        if (name.Length == 0) return base.GetInstanceName();

        return $"{base.GetInstanceName()} - {GetButtonLabel(name)}";
    }

    public override string GetMenuLabel()
    {
        string name = GetConfigValue<string>("SelectedCollection");

        return name.Length > 0 ? GetButtonLabel(name) : base.GetMenuLabel();
    }

    public override string? GetMenuAltText()
    {
        string name = GetConfigValue<string>("SelectedCollection");

        if (name.Length == 0) return null;

        DalamudProfile? profile = FindProfile(name);

        if (profile is null) return null;

        return I18N.Translate(
            profile.Value.IsEnabled
                ? "Widget.PluginCollections.State.Enabled"
                : "Widget.PluginCollections.State.Disabled"
        );
    }

    /// <summary>
    /// Returns the custom label of this widget, or the given collection name
    /// when no custom label is set.
    /// </summary>
    private string GetButtonLabel(string fallback)
    {
        string label = GetConfigValue<string>("ButtonLabel").Trim();

        return label.Length > 0 ? label : fallback;
    }

    /// <summary>
    /// True if the icon of this widget is still the default one. The icon is
    /// then used to indicate whether the collection is enabled.
    /// </summary>
    private bool IsUsingDefaultIcon()
    {
        return CvarIconType() == IconTypeFontAwesome && CvarFontAwesomeIcon() == DefaultFontAwesomeIcon;
    }

    private void RefreshProfiles()
    {
        _lastUpdate = DateTime.UtcNow;
        _profiles   = DalamudProfileProvider.GetProfiles();
    }

    private DalamudProfile? FindProfile(string name)
    {
        foreach (DalamudProfile profile in _profiles) {
            if (profile.Name == name) return profile;
        }

        return null;
    }

    private void OnClick(Node _)
    {
        string name = GetConfigValue<string>("SelectedCollection");

        if (name.Length == 0) return;

        DalamudProfile? profile = FindProfile(name);

        if (profile is null) return;

        // Dalamud expects the collection name in quotes:
        // /xlenablecollection "Collection Name"
        SendCommand($"{(profile.Value.IsEnabled ? "/xldisablecollection" : "/xlenablecollection")} \"{name}\"");
    }

    /// <summary>
    /// Opens Dalamud's plugin installer - where collections are managed - using
    /// the official API instead of a chat command.
    /// </summary>
    private static void OpenCollectionsManager()
    {
        if (Framework.DalamudPlugin.OpenPluginInstallerTo(PluginInstallerOpenKind.InstalledPlugins)) return;

        SendCommand("/xlplugins");
    }

    private static void SendCommand(string command)
    {
        try {
            Framework.Service<IChatSender>().Send(command);
        } catch (Exception e) {
            Logger.Warning($"Failed to run command \"{command}\": {e.Message}");
        }
    }
}
