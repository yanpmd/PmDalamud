using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbra.Conditions;
using Umbra.Windows;
using Umbra.Windows.Dialogs;

namespace Umbra.CustomHotbar;

/// <summary>
/// Owns all custom hotbar instances: their configuration, their native windows and their hotkeys.
/// </summary>
/// <remarks>
/// <para>
/// The bars are independent of each other, just like the auxiliary toolbars are. Each one has its own
/// name, slots, key bindings, position, background and display condition, and none of them is partitioned
/// by zone.
/// </para>
/// <para>
/// All instances are stored in a single cvar as a JSON array, keyed by a stable id. The native addon of
/// an instance is only created while that instance is enabled and the player is logged in.
/// </para>
/// </remarks>
[Service]
internal sealed unsafe class CustomHotbarManager : IDisposable {
    private const string CvarName = "CustomHotbar.Data";

    // Number of frames a freshly positioned bar stays visible before auto-hide is allowed to kick in.
    private const int PositionGraceFrameCount = 10;

    // An empty value means that this feature has never been configured before. A configured but empty
    // list is stored as "[]", which lets the user delete every bar without them coming back on reload.
    [ConfigVariable(CvarName)] private static string CustomHotbarData { get; set; } = "";

    private readonly List<CustomHotbarConfig>    _configs     = [];
    private readonly Dictionary<string, Runtime> _runtimes    = [];
    private readonly IClientState                 _clientState;

    /// <summary>
    /// Raised when a bar was added, removed or copied. The argument suggests which bar should become
    /// active in the settings window, or null when the active bar was removed.
    /// </summary>
    public event Action<string?>? OnBarsChanged;

    /// <summary>
    /// Raised when the user changed the contents of the slots of the given bar.
    /// </summary>
    public event Action<string>? OnSlotsChanged;

    /// <summary>
    /// All configured bars, in the order they were created.
    /// </summary>
    public IReadOnlyList<CustomHotbarConfig> All => _configs;

    public CustomHotbarManager(IClientState clientState) {
        _clientState = clientState;

        ConfigManager.CvarChanged += OnCvarChanged;

        Load();
    }

    public void Dispose() {
        ConfigManager.CvarChanged -= OnCvarChanged;

        foreach (string id in _runtimes.Keys.ToArray()) {
            DisposeRuntime(id);
        }

        _runtimes.Clear();
    }

    /// <summary>
    /// Returns the configuration with the given id, or null when it no longer exists.
    /// </summary>
    public CustomHotbarConfig? GetConfig(string id)
        => _configs.FirstOrDefault(config => config.Id == id);

    /// <summary>
    /// Returns true when a bar with the given name already exists.
    /// </summary>
    public bool NameExists(string name)
        => _configs.Any(config => config.Name == name);

    /// <summary>
    /// Adds a new, empty bar.
    /// </summary>
    public CustomHotbarConfig CreateBar() {
        string name = I18N.Translate("Settings.CustomHotbarModule.DefaultBarName", 1);

        for (int i = 2; NameExists(name); i++) {
            name = I18N.Translate("Settings.CustomHotbarModule.DefaultBarName", i);
        }

        var config = new CustomHotbarConfig {
            Name = name,
        };

        config.Normalize();

        _configs.Add(config);

        Persist();

        OnBarsChanged?.Invoke(config.Id);

        return config;
    }

    /// <summary>
    /// Adds a copy of an existing bar, including its slots and key bindings.
    /// </summary>
    public CustomHotbarConfig? CreateCopyOfBar(string id) {
        CustomHotbarConfig? source = GetConfig(id);
        if (source is null) return null;

        string name = $"{source.Name} (2)";

        for (int i = 3; NameExists(name); i++) {
            name = $"{source.Name} ({i})";
        }

        CustomHotbarConfig copy = source.Clone(newName: name);

        _configs.Add(copy);

        Persist();

        OnBarsChanged?.Invoke(copy.Id);

        return copy;
    }

    /// <summary>
    /// Removes a bar, optionally asking the user for confirmation first.
    /// </summary>
    public void DeleteBar(string id, bool confirm = true) {
        CustomHotbarConfig? config = GetConfig(id);
        if (config is null) return;

        if (!confirm) {
            DeleteBarInternal(config);

            return;
        }

        Framework.Service<WindowManager>().Present<ConfirmationWindow>(
            "DeleteCustomHotbarConfirmation",
            new(
                I18N.Translate("Settings.CustomHotbarModule.DeleteBarConfirmation.Title", config.Name),
                I18N.Translate("Settings.CustomHotbarModule.DeleteBarConfirmation.Message", config.Name),
                I18N.Translate("Delete"),
                I18N.Translate("Cancel"),
                I18N.Translate("Settings.CustomHotbarModule.DeleteBarConfirmation.Hint")
            ),
            window => {
                if (window.Confirmed) DeleteBarInternal(config);
            }
        );
    }

    private void DeleteBarInternal(CustomHotbarConfig config) {
        DisposeRuntime(config.Id);

        _configs.Remove(config);

        Persist();

        OnBarsChanged?.Invoke(null);
    }

    /// <summary>
    /// Writes all configurations to the config file.
    /// </summary>
    public void Persist() {
        try {
            ConfigManager.Set(CvarName, JsonConvert.SerializeObject(_configs));
        }
        catch (Exception e) {
            Logger.Error($"Failed to save the custom hotbar configuration: {e.Message}");
        }
    }

    /// <summary>
    /// Alias of <see cref="Persist"/>.
    /// </summary>
    public void Save() => Persist();

    /// <summary>
    /// Binds a packed key to the given slot of the given bar.
    /// </summary>
    public void SetSlotHotkey(string barId, int index, int packed) {
        CustomHotbarConfig? config = GetConfig(barId);
        if (config is null || index < 0 || index >= config.Slots.Count) return;

        (VirtualKey key, KeyModifierFlag modifier) = CustomHotbarKeybind.Unpack(packed);

        config.Slots[index].Hotkey         = key;
        config.Slots[index].HotkeyModifier = modifier;

        if (_runtimes.TryGetValue(barId, out Runtime? runtime)) {
            runtime.Addon?.SetSlotHotkey(index, key, modifier);
        }

        Persist();
    }

    /// <summary>
    /// Returns the packed key binding of a slot.
    /// </summary>
    public int GetSlotHotkey(string barId, int index) {
        CustomHotbarConfig? config = GetConfig(barId);
        if (config is null || index < 0 || index >= config.Slots.Count) return 0;

        return CustomHotbarKeybind.Pack(config.Slots[index].Hotkey, config.Slots[index].HotkeyModifier);
    }

    /// <summary>
    /// Sets whether the bound key is left for the game to handle as well.
    /// </summary>
    public void SetSlotKeyPassthrough(string barId, int index, bool passthrough) {
        CustomHotbarConfig? config = GetConfig(barId);
        if (config is null || index < 0 || index >= config.Slots.Count) return;

        config.Slots[index].KeyPassthrough = passthrough;

        Persist();
    }

    /// <summary>
    /// Returns whether the bound key is left for the game to handle as well.
    /// </summary>
    public bool GetSlotKeyPassthrough(string barId, int index) {
        CustomHotbarConfig? config = GetConfig(barId);
        if (config is null || index < 0 || index >= config.Slots.Count) return false;

        return config.Slots[index].KeyPassthrough;
    }

    /// <summary>
    /// Removes the key binding of the given slot of the given bar.
    /// </summary>
    public void ClearSlotHotkey(string barId, int index)
        => SetSlotHotkey(barId, index, 0);

    [OnTick]
    public void Update() {
        // The game is not able to show custom windows before the player is logged in.
        if (!_clientState.IsLoggedIn) return;

        // QoLBarEx order: sample the keyboard, consume the registrations of the previous frame, then
        // register the slots eligible for the next one. Nothing on this path rebuilds UI or writes the
        // configuration, so pressing a key cannot cause a refresh.
        CustomHotbarKeybind.Tick();
        CustomHotbarKeybind.Consume(OnHotkeyActivated);

        RegisterHotkeys();

        foreach (CustomHotbarConfig config in _configs.ToArray()) {
            SyncBar(config, GetRuntime(config.Id));
        }
    }

    /// <summary>
    /// Registers the hotkeys of all enabled bars for the next frame.
    /// </summary>
    /// <remarks>
    /// Only a lightweight list of references is rebuilt every frame. Bar windows, slot nodes and their
    /// cooldown/charge/quantity/cost/key-label parts are persistent and updated in place.
    /// </remarks>
    private void RegisterHotkeys() {
        foreach (CustomHotbarConfig config in _configs) {
            if (!config.IsEnabled) continue;

            // Only bars that are currently shown answer to their hotkeys. Auto-hide is excluded on
            // purpose: a bar that is merely faded out because the cursor moved away must stay usable.
            if (!ConditionSetManager.IsMatchSafe(config.ConditionSetId)) continue;

            for (int i = 0; i < config.Slots.Count; i++) {
                CustomHotbarSlot slot = config.Slots[i];

                int packed = CustomHotbarKeybind.Pack(slot.Hotkey, slot.HotkeyModifier);
                if (packed == 0) continue;

                CustomHotbarKeybind.Register(config.Id, i, packed, slot.KeyPassthrough);
            }
        }
    }

    /// <summary>
    /// Executes the slot whose hotkey fired.
    /// </summary>
    private void OnHotkeyActivated(string barId, int index) {
        if (_runtimes.TryGetValue(barId, out Runtime? runtime)) {
            runtime.Addon?.ExecuteSlot(index);
        }
    }

    /// <summary>
    /// Brings one bar in sync with its configuration.
    /// </summary>
    private void SyncBar(CustomHotbarConfig config, Runtime runtime) {
        if (!config.IsEnabled) {
            CloseWindow(runtime);

            return;
        }

        EnsureAddon(config, runtime);

        CustomHotbarAddon? addon = runtime.Addon;
        if (addon is null) return;

        if (runtime.AppliedSlotCount != config.SlotCount) {
            runtime.AppliedSlotCount = config.SlotCount;

            addon.SetSlotCount(config.SlotCount);
            addon.LoadSlots(config.Slots);
        }

        addon.SetSlotsPerRow(config.SlotsPerRow);
        addon.SetLocked(config.IsLocked);
        addon.SetHideEmptySlots(config.HideEmptySlots);
        addon.SetShowBackground(config.ShowBackground);
        addon.UpdateSlots();

        UpdatePosition(config, runtime);

        if (runtime.PositionGraceFrames > 0) runtime.PositionGraceFrames--;

        bool visible = ConditionSetManager.IsMatchSafe(config.ConditionSetId);

        if (visible && config.EnableAutoHide) {
            visible = IsCursorNearBar(config, runtime);
        }

        ApplyVisibility(addon, runtime, visible);
    }

    /// <summary>
    /// Applies a visibility change only after it has been stable for two frames.
    /// </summary>
    /// <remarks>
    /// Conditions and cursor proximity can flip for a single frame while the game opens a menu. Toggling
    /// the root node on such a transient frame makes the whole bar flicker.
    /// </remarks>
    private static void ApplyVisibility(CustomHotbarAddon addon, Runtime runtime, bool visible) {
        // The first evaluation has to be applied immediately. Going through the de-bounce below would
        // keep the default "visible" state for a couple of frames, which shows every bar on load even
        // when its display condition is not met.
        if (!runtime.VisibilityInitialized) {
            runtime.VisibilityInitialized = true;
            runtime.StableVisible         = visible;
            runtime.PendingVisible        = visible;
            runtime.PendingFrames         = 0;

            addon.SetContentVisible(visible);

            return;
        }

        if (visible == runtime.StableVisible) {
            runtime.PendingVisible = visible;
            runtime.PendingFrames  = 0;
        }
        else if (visible != runtime.PendingVisible) {
            runtime.PendingVisible = visible;
            runtime.PendingFrames  = 1;
        }
        else {
            runtime.PendingFrames++;

            if (runtime.PendingFrames >= 2) {
                runtime.StableVisible = visible;
                runtime.PendingFrames = 0;
            }
        }

        addon.SetContentVisible(runtime.StableVisible);
    }

    /// <summary>
    /// Creates the window of the bar if it does not exist yet, or reopens it after it was closed.
    /// </summary>
    private void EnsureAddon(CustomHotbarConfig config, Runtime runtime) {
        if (runtime.Addon is not null) {
            if (runtime.Addon.IsOpen || runtime.ReopenPending) return;

            // Closing an addon makes the game destroy it, so it can only be opened again once the game has
            // finished cleaning it up. Reopening it right away would be a no-op.
            runtime.ReopenPending = true;

            Framework.DalamudFramework.RunOnTick(() => {
                    runtime.ReopenPending = false;

                    if (!_configs.Contains(config) || !config.IsEnabled) return;
                    if (runtime.Addon is null || runtime.Addon.IsOpen) return;

                    runtime.Addon.Open();
                    runtime.Addon.SetSlotCount(config.SlotCount);
                    runtime.Addon.LoadSlots(config.Slots);

                    runtime.AppliedSlotCount = config.SlotCount;
                },
                delayTicks: 2
            );

            return;
        }

        var addon = new CustomHotbarAddon {
            InternalName            = GetAddonName(config.Id),
            BarId                  = config.Id,
            Title                  = "UmbraEx",
            ContentPadding         = Vector2.Zero,
            OpenWindowSoundEffectId = 0,

            // The position of the bar is fully controlled by the configuration, so the game must not
            // restore the position it was closed at.
            RememberClosePosition = false,
            Size                 = CustomHotbarAddon.GetWindowSize(config.SlotCount, config.SlotsPerRow),

            // A bar is a permanent part of the UI, not a window the player can dismiss. Escape closes
            // unfocused addons, which destroys the node tree and forces a rebuild a few frames later -
            // that is the flicker. Never take focus, never grab it on show, never close on escape.
            RespectCloseAll            = false,
            DisableFocusability        = true,
            DisableFocusOnShow         = true,
            DisableUnfocusedCloseOnEsc = true,
        };

        addon.OnSlotsChanged += OnAddonSlotsChanged;

        runtime.Addon = addon;

        addon.Open();
        addon.SetSlotCount(config.SlotCount);
        addon.LoadSlots(config.Slots);

        runtime.AppliedSlotCount = config.SlotCount;
    }

    /// <summary>
    /// Closes the window of the bar without destroying it, so it can be opened again later.
    /// </summary>
    private static void CloseWindow(Runtime runtime) {
        if (runtime.Addon is null) return;
        if (!runtime.Addon.IsOpen) return;

        // Closing the addon destroys its nodes, which is only allowed on the main thread.
        if (Dalamud.Utility.ThreadSafety.IsMainThread) {
            runtime.Addon.Close();
        }
    }

    /// <summary>
    /// Moves a bar to the position that is described by its configuration.
    /// </summary>
    private static void UpdatePosition(CustomHotbarConfig config, Runtime runtime) {
        CustomHotbarAddon? addon = runtime.Addon;
        if (addon is null) return;

        // SetPosition is a silent no-op on a closed addon, so remembering the position here would pin the
        // bar to the centered default position forever.
        if (!addon.IsOpen) return;

        Vector2 screenSize = new(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);

        // Right after a reload or a zone change the stage reports a zero-sized screen for a few frames.
        // Computing an anchor against that would store a bogus position that never gets corrected.
        if (screenSize.X <= 0 || screenSize.Y <= 0) return;

        Vector2 barSize = CustomHotbarAddon.GetContentSize(config.SlotCount, config.SlotsPerRow);

        float x = config.XAlign switch {
            "Left"   => config.XPos,
            "Right"  => screenSize.X - config.XPos - barSize.X,
            _        => (screenSize.X / 2.0f - barSize.X / 2.0f) + config.XPos,
        };

        float y = config.YAlign switch {
            "Top"    => config.YPos,
            "Bottom" => screenSize.Y - config.YPos - barSize.Y,
            _        => (screenSize.Y / 2.0f - barSize.Y / 2.0f) + config.YPos,
        };

        // SetPosition takes whole units, so the target is rounded to compare it against the value the game
        // reports back.
        Vector2 position = new(MathF.Round(x), MathF.Round(y));

        // SetPosition triggers a layout pass of the native addon, so it is only called when the target
        // changed or when the game moved the window somewhere else on its own.
        if (runtime.HasPosition && position == runtime.Position && addon.WindowPosition == position) return;

        // The game can still report its default (centered) position for a few frames after a reload.
        // Keep the bar visible until the geometry has settled, so auto-hide never locks it away with
        // a hover area that does not match where the bar actually ended up.
        if (!runtime.HasPosition) runtime.PositionGraceFrames = PositionGraceFrameCount;

        runtime.HasPosition = true;
        runtime.Position    = position;

        addon.SetWindowPosition(position);
    }

    /// <summary>
    /// Returns true when the mouse cursor is close enough to the bar to show it again.
    /// </summary>
    private static bool IsCursorNearBar(CustomHotbarConfig config, Runtime runtime) {
        CustomHotbarAddon? addon = runtime.Addon;

        // Without a usable window or a settled position there is no reliable hit area. Hiding the bar
        // on such a frame would leave no way for the cursor to bring it back after a reload.
        if (addon is null || !addon.IsOpen || !runtime.HasPosition || runtime.PositionGraceFrames > 0) {
            return true;
        }

        ImGuiViewportPtr viewport = ImGui.GetMainViewport();

        Vector2 screenSize = new(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);
        if (screenSize.X <= 0 || screenSize.Y <= 0) return true;

        float scaleX = viewport.WorkSize.X / screenSize.X;
        float scaleY = viewport.WorkSize.Y / screenSize.Y;
        if (scaleX <= 0 || scaleY <= 0) return true;

        Vector2 mouse = ImGui.GetMousePos();

        // The mouse position is in screen pixels, while the bar is placed in the coordinate space of the
        // game user interface, so it has to be converted first.
        Vector2 mouseInUi = new(
            (mouse.X - viewport.WorkPos.X) / scaleX,
            (mouse.Y - viewport.WorkPos.Y) / scaleY
        );

        Vector2 barSize = CustomHotbarAddon.GetContentSize(config.SlotCount, config.SlotsPerRow);

        // Use the position the game actually reports instead of the requested one. When the game moves
        // the window on its own the two differ, and a hover area built from the target would sit
        // somewhere the bar is not, making it impossible to unhide.
        Vector2 position = addon.WindowPosition;

        const float margin = 32.0f;

        return mouseInUi.X >= position.X - margin
            && mouseInUi.X <= position.X + barSize.X + margin
            && mouseInUi.Y >= position.Y - margin
            && mouseInUi.Y <= position.Y + barSize.Y + margin;
    }

    /// <summary>
    /// Called when the user has changed the content of specific slots of a bar.
    /// </summary>
    /// <remarks>
    /// Only the slots that were actually edited are re-read. Reading the whole bar at once made a single
    /// unreadable slot wipe every other slot along with it.
    /// </remarks>
    private void OnAddonSlotsChanged(string barId, int[] indices) {
        CustomHotbarConfig? config = GetConfig(barId);

        if (config is null) return;
        if (!_runtimes.TryGetValue(barId, out Runtime? runtime) || runtime.Addon is null) return;

        var changed = false;

        foreach (int index in indices) {
            if (index < 0 || index >= config.Slots.Count) continue;

            CustomHotbarSlot? captured = runtime.Addon.CaptureSlot(index);

            // A slot that claims to have content but cannot report it keeps what is stored, instead of
            // being overwritten with an empty value.
            if (captured is null) {
                Logger.Warning($"[CustomHotbar] could not read slot {index} of bar '{barId}', keeping stored value");

                continue;
            }

            CustomHotbarSlot stored = config.Slots[index];

            if (stored.Kind == captured.Kind && stored.Id == captured.Id && stored.RawType == captured.RawType) continue;

            stored.Kind    = captured.Kind;
            stored.RawType = captured.RawType;
            stored.Id      = captured.Id;
            changed        = true;
        }

        if (!changed) return;

        // The live nodes already contain the new content, so they must not be written to again.
        runtime.Addon.SyncSlots(config.Slots);

        Persist();

        OnSlotsChanged?.Invoke(barId);
    }

    /// <summary>
    /// Reads the configurations from the config file.
    /// </summary>
    private void Load() {
        string raw       = CustomHotbarData;
        bool   isFresh   = string.IsNullOrWhiteSpace(raw);
        bool   isLegacy  = !isFresh && raw.TrimStart().StartsWith("{");

        Reconcile(ReadConfigs(raw));

        // On a fresh install the settings window would otherwise be empty. This only happens on the
        // initial load, so deleting every bar later on stays possible.
        if (isFresh) {
            var config = new CustomHotbarConfig {
                Name = I18N.Translate("Settings.CustomHotbarModule.DefaultBarName", 1),
            };

            config.Normalize();

            _configs.Add(config);
        }

        // Write the result back when it differs from what was stored: a fresh install gets its default
        // bar, the single-object format of the first version is migrated, and repairs applied by
        // Normalize() (such as leftover native types on emptied slots) become permanent.
        string normalized = JsonConvert.SerializeObject(_configs);

        if (isFresh || isLegacy || normalized != raw) Persist();
    }

    /// <summary>
    /// Reads a list of configurations, also accepting the single-object format used by the first
    /// version of this feature.
    /// </summary>
    private static List<CustomHotbarConfig> ReadConfigs(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        try {
            JToken token = JToken.Parse(raw);

            List<CustomHotbarConfig>? data = token switch {
                JArray array => array.ToObject<List<CustomHotbarConfig>>(),
                JObject      => [token.ToObject<CustomHotbarConfig>()!],
                _            => null,
            };

            if (data is null) return [];

            foreach (CustomHotbarConfig config in data) {
                if (config is null) continue;

                // The old format had no identity or name, so give it one on migration.
                if (string.IsNullOrWhiteSpace(config.Id)) {
                    config.Id = $"hotbar{Guid.NewGuid():N}";
                }

                if (string.IsNullOrWhiteSpace(config.Name)) {
                    config.Name = I18N.Translate("Settings.CustomHotbarModule.DefaultBarName", 1);
                }

                config.Normalize();
            }

            return data.Where(config => config is not null && !string.IsNullOrWhiteSpace(config.Id)).ToList();
        }
        catch (Exception e) {
            Logger.Error($"Failed to read the custom hotbar configuration: {e.Message}");

            return [];
        }
    }

    /// <summary>
    /// Merges the given configurations into the live list and cleans up the windows of removed bars.
    /// </summary>
    private void Reconcile(List<CustomHotbarConfig> data) {
        foreach (CustomHotbarConfig incoming in data) {
            CustomHotbarConfig? existing = _configs.FirstOrDefault(config => config.Id == incoming.Id);

            if (existing is null) {
                // Keeps the slot list in sync with the slot count when the stored data is older than the
                // configured layout.
                incoming.Normalize();

                _configs.Add(incoming);
            }
            else {
                existing.Update(incoming);
            }
        }

        foreach (CustomHotbarConfig config in _configs.ToArray()) {
            if (data.Any(candidate => candidate.Id == config.Id)) continue;

            _configs.Remove(config);

            DisposeRuntime(config.Id);
        }
    }

    private void OnCvarChanged(string name) {
        if (name != CvarName) return;

        Reconcile(ReadConfigs(CustomHotbarData));
    }

    /// <summary>
    /// Returns the runtime state of a bar, creating it when needed.
    /// </summary>
    private Runtime GetRuntime(string id) {
        if (!_runtimes.TryGetValue(id, out Runtime? runtime)) {
            runtime = new Runtime();
            _runtimes[id] = runtime;
        }

        return runtime;
    }

    /// <summary>
    /// Destroys the native window of a bar and drops its runtime state.
    /// </summary>
    private void DisposeRuntime(string id) {
        if (!_runtimes.Remove(id, out Runtime? runtime)) return;
        if (runtime.Addon is null) return;

        runtime.Addon.OnSlotsChanged -= OnAddonSlotsChanged;

        // Closing the addon destroys its nodes, which is only allowed on the main thread.
        if (Dalamud.Utility.ThreadSafety.IsMainThread) {
            runtime.Addon.Dispose();
        }

        runtime.Addon = null;
    }

    /// <summary>
    /// Derives a stable, unique and short native addon name from the id of a bar.
    /// </summary>
    private static string GetAddonName(string id) {
        uint hash = 2166136261;

        foreach (char c in id) {
            hash ^= c;
            hash *= 16777619;
        }

        return $"UmbraExCHB{hash:X8}";
    }

    /// <summary>
    /// The non-persisted state of one bar.
    /// </summary>
    private sealed class Runtime {
        public CustomHotbarAddon? Addon;
        public bool              ReopenPending;
        public bool              HasPosition;
        public int               PositionGraceFrames;
        public bool              VisibilityInitialized;
        public bool              StableVisible  = true;
        public bool              PendingVisible = true;
        public int               PendingFrames;
        public int               AppliedSlotCount = -1;
        public Vector2           Position;
    }
}
