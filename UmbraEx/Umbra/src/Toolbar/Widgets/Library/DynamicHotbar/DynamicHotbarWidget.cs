using Umbra.Conditions;

namespace Umbra.Widgets.Library.DynamicHotbar;

/// <summary>
/// A hotbar whose contents are different for every zone the player visits.
/// </summary>
/// <remarks>
/// <para>
/// The widget itself only shows the name of the current zone and opens a native game window that
/// contains the actual slots. The slots are implemented with KamiToolKit's <c>HotbarNode</c>, which
/// means that actions, items and macros can be dragged into them from the game's own user interface
/// and that clicking a slot uses the item, casts the action or runs the macro.
/// </para>
/// <para>
/// The contents of the hotbar are stored per territory, so entering a new zone starts with an empty
/// hotbar while returning to a known one restores whatever was configured for it.
/// </para>
/// </remarks>
[ToolbarWidget(
    "DynamicHotbar",
    "Widget.DynamicHotbar.Name",
    "Widget.DynamicHotbar.Description",
    ["hotbar", "action", "ability", "item", "macro", "dynamic", "zone"]
)]
internal sealed partial class DynamicHotbarWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    protected override uint DefaultGameIconId => 14;

    private const string CvarNameNumSlots = "NumSlots";
    private const string CvarNameSlotData = "SlotData";

    private readonly DynamicHotbarSlotStore _store = new();

    private DynamicHotbarAddon? _addon;
    private uint                _territoryType;
    private bool                _hasTerritoryType;
    private bool                _windowRequested;
    private bool                _reopenPending;
    private int                 _appliedSlotCount;
    private string              _zoneName = string.Empty;

    protected override void OnLoad() {
        _store.Decode(GetConfigValue<string>(CvarNameSlotData));
        _appliedSlotCount = GetConfigValue<int>(CvarNameNumSlots);

        // The widget does not use an Umbra popup, so the mouse handling has to be set up manually.
        Node.OnMouseDown += _ => {
            if (Node.IsDisabled) return;

            _windowRequested = !_windowRequested;
        };
    }

    protected override void OnUnload() {
        if (_addon is null) return;

        _addon.OnSlotsChanged -= OnSlotsChanged;
        _addon.OnClosed       -= OnWindowClosed;

        // Closing the addon destroys its nodes, which is only allowed on the main thread.
        if (Dalamud.Utility.ThreadSafety.IsMainThread) {
            _addon.Dispose();
        }

        _addon = null;
    }

    protected override void OnDraw() {
        uint territory = ConditionServices.ClientState?.TerritoryType ?? 0;

        if (!_hasTerritoryType || territory != _territoryType) {
            _hasTerritoryType = true;
            _territoryType    = territory;

            LoadTerritorySlots();
        }

        int slotCount = GetConfigValue<int>(CvarNameNumSlots);

        if (_appliedSlotCount != slotCount) {
            _appliedSlotCount = slotCount;

            _addon?.SetSlotCount(slotCount);
            LoadTerritorySlots();
        }

        if (_windowRequested) {
            EnsureWindow();
        }
        else if (_addon is not null && _addon.IsOpen) {
            _addon.Close();
        }

        string zoneName = ConditionServices.GetZoneName(territory);

        if (zoneName != _zoneName) {
            _zoneName = zoneName;

            SetText(zoneName);

            _addon?.SetSubtitle(zoneName);
        }
    }

    private void EnsureWindow() {
        if (_addon is null) {
            DynamicHotbarAddon addon = new() {
                InternalName = "UmbraExDynamicHotbar",
                Title       = I18N.Translate("Widget.DynamicHotbar.WindowTitle"),
                Size        = DynamicHotbarAddon.GetWindowSize(_appliedSlotCount),
            };

            addon.OnSlotsChanged += OnSlotsChanged;
            addon.OnClosed       += OnWindowClosed;

            _addon = addon;

            addon.Open();
            addon.SetSubtitle(_zoneName);
            addon.LoadSlots(_store.GetSlots(_territoryType));

            return;
        }

        if (_addon.IsOpen || _reopenPending) return;

        // Closing an addon makes the game destroy it, so it can only be opened again once the game has
        // finished cleaning it up. Reopening it right away would be a no-op.
        _reopenPending = true;

        Framework.DalamudFramework.RunOnTick(() => {
                _reopenPending = false;

                if (_addon is null || !_windowRequested) return;
                if (_addon.IsOpen) return;

                _addon.Open();
                _addon.SetSubtitle(_zoneName);
                _addon.LoadSlots(_store.GetSlots(_territoryType));
            },
            delayTicks: 2
        );
    }

    private void LoadTerritorySlots() {
        _addon?.LoadSlots(_store.GetSlots(_territoryType));
    }

    private void OnSlotsChanged() {
        if (_addon is null) return;

        SaveSlots(_addon.CaptureSlots());
    }

    private void OnWindowClosed() {
        _windowRequested = false;

        if (_addon is null) return;

        SaveSlots(_addon.CaptureSlots());
    }

    private void SaveSlots(List<DynamicHotbarSlot> slots) {
        _store.SetSlots(_territoryType, slots);

        try {
            SetConfigValue(CvarNameSlotData, _store.Encode());
        }
        catch (Exception e) {
            Logger.Error(e.Message);
        }
    }
}
