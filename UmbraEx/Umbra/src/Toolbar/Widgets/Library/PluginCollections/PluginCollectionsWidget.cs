using Dalamud.Interface;
using Dalamud.Plugin;
using System.Threading.Tasks;

namespace Umbra.Widgets.Library.PluginCollections;

[ToolbarWidget(
    "PluginCollections",
    "Widget.PluginCollections.Name",
    "Widget.PluginCollections.Description",
    ["plugin", "collection", "profile", "dalamud", "group", "toggle"]
)]
internal sealed partial class PluginCollectionsWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    public override MenuPopup Popup { get; } = new();

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
        Popup.OnPopupOpen  += OnPopupOpened;
        Popup.OnPopupClose += ClearPopup;

        Node.OnRightClick += _ => OpenCollectionsManager();

        // Enabling or disabling a collection loads/unloads plugins, which
        // Dalamud announces through this event. Using it means the widget no
        // longer has to poll to pick up the new state.
        Framework.DalamudPlugin.ActivePluginsChanged += OnActivePluginsChanged;
    }

    protected override void OnUnload()
    {
        Popup.OnPopupOpen  -= OnPopupOpened;
        Popup.OnPopupClose -= ClearPopup;

        Framework.DalamudPlugin.ActivePluginsChanged -= OnActivePluginsChanged;
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args)
    {
        Framework.DalamudFramework.Run(
            () => {
                RefreshProfiles();

                if (Popup.IsOpen) RebuildPopup();
            }
        );
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

    protected override void OnDraw()
    {
        if (DateTime.UtcNow - _lastUpdate > TimeSpan.FromMilliseconds(500)) {
            RefreshProfiles();
        }

        SetText(GetLabelText());

        Node.Tooltip = GetConfigValue<bool>("ShowTooltip") ? I18N.Translate("Widget.PluginCollections.Tooltip") : null;
    }

    private void OnPopupOpened()
    {
        Framework.DalamudFramework.Run(
            () => {
                RefreshProfiles();
                RebuildPopup();
            }
        );
    }

    private void ClearPopup()
    {
        Framework.DalamudFramework.Run(() => Popup.Clear(true));
    }

    private void RefreshProfiles()
    {
        _lastUpdate = DateTime.UtcNow;
        _profiles   = DalamudProfileProvider.GetProfiles();
    }

    private void RebuildPopup()
    {
        Popup.Clear(true);

        if (!DalamudProfileProvider.IsSupported || _profiles.Count == 0) {
            Popup.Add(
                new MenuPopup.Button("PluginCollections_Unavailable") {
                    Label      = I18N.Translate("Widget.PluginCollections.Unavailable"),
                    IsDisabled = true,
                }
            );

            return;
        }

        bool showDisabled = GetConfigValue<bool>("ShowDisabledCollections");
        var  sortIndex    = 0;

        foreach (DalamudProfile profile in _profiles) {
            if (!profile.IsEnabled && !showDisabled) continue;

            DalamudProfile entry = profile;

            MenuPopup.Button button = new($"PluginCollection_{profile.Name}") {
                Label     = profile.Name,
                AltText   = I18N.Translate(
                    profile.IsEnabled
                        ? "Widget.PluginCollections.State.Enabled"
                        : "Widget.PluginCollections.State.Disabled"
                ),
                Selected  = profile.IsEnabled,
                SortIndex = sortIndex++,
                OnClick   = () => ToggleCollection(entry),
            };

            if (!profile.IsEnabled) {
                button.TextColor    = new("Widget.PopupMenuTextMuted");
                button.AltTextColor = new("Widget.PopupMenuTextMuted");
            }

            Popup.Add(button);
        }
    }

    private void ToggleCollection(DalamudProfile profile)
    {
        // Dalamud expects the collection name in quotes:
        // /xlenablecollection "Collection Name"
        SendCommand($"{(profile.IsEnabled ? "/xldisablecollection" : "/xlenablecollection")} \"{profile.Name}\"");

        // Switching a collection is asynchronous inside Dalamud, so poll the
        // state a few times until it has settled.
        _ = RefreshAfterToggle();
    }

    private async Task RefreshAfterToggle()
    {
        // The ActivePluginsChanged event handles the normal case. This is only a
        // safety net for collections whose state is read from Dalamud's config
        // file, or for toggles that don't change any plugin's load state.
        await Task.Delay(1500);

        await Framework.DalamudFramework.Run(
            () => {
                RefreshProfiles();

                if (Popup.IsOpen) RebuildPopup();
            }
        );
    }

    private string GetLabelText()
    {
        if (!GetConfigValue<bool>("ShowLabel")) return string.Empty;

        List<string> enabled = _profiles.Where(profile => profile.IsEnabled).Select(profile => profile.Name).ToList();

        return enabled.Count > 0 ? string.Join(", ", enabled) : I18N.Translate("Widget.PluginCollections.None");
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
