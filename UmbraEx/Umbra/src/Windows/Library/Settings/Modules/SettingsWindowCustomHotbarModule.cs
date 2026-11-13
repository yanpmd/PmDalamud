using Lumina.Excel.Sheets;
using Umbra.Conditions;
using Umbra.Conditions.Builtin;
using Umbra.CustomHotbar;
using ActionSheet = Lumina.Excel.Sheets.Action;
using ItemSheet = Lumina.Excel.Sheets.Item;
using KeyItemSheet = Lumina.Excel.Sheets.EventItem;

namespace Umbra.Windows.Settings.Modules;

/// <summary>
/// Lists all custom hotbar instances and edits the configuration of the selected one.
/// </summary>
/// <remarks>
/// <para>
/// The bars follow the same pattern as the auxiliary toolbars: they can be added, copied, renamed and
/// removed independently, and every one of them owns its own slots, key bindings, position and display
/// condition.
/// </para>
/// <para>
/// Following QoLBarEx, nodes are created once and then updated in place every frame. No input event,
/// key press or drag and drop rebuilds the editor, so nothing flickers and focused controls keep their
/// focus.
/// </para>
/// </remarks>
internal sealed class SettingsWindowCustomHotbarModule : SettingsWindowModule {
    public override string Name  => I18N.Translate("Settings.CustomHotbarModule.Name");
    public override int    Order => 1;

    protected override string UdtResourceName => "umbra.windows.settings.modules.custom_hotbar_module.xml";

    private CustomHotbarManager Manager => Framework.Service<CustomHotbarManager>();

    private Node?   _barList;
    private Node?   _content;
    private string  _activeBarId = string.Empty;
    private bool    _structureDirty;

    private readonly Dictionary<string, Node>          _barRows        = [];
    private readonly Dictionary<string, string>        _lastNames      = [];
    private readonly List<HotkeyInputNode>             _hotkeyInputs   = [];
    private readonly List<string>                      _lastSlotLabels = [];

    protected override void OnOpen() {
        _barList = RootNode.QuerySelector<Node>("#bar-list");
        _content = RootNode.QuerySelector<Node>("#content");

        RootNode.QuerySelector("#add-bar")!.OnClick += _ => Manager.CreateBar();

        Manager.OnBarsChanged += OnBarsChanged;

        _activeBarId = Manager.All.FirstOrDefault()?.Id ?? string.Empty;

        BuildBarList();
        BuildContent();
    }

    protected override void OnClose() {
        Manager.OnBarsChanged -= OnBarsChanged;

        _barRows.Clear();
        _lastNames.Clear();
        _hotkeyInputs.Clear();
        _lastSlotLabels.Clear();

        _barList          = null;
        _content          = null;
        _activeBarId      = string.Empty;
        _structureDirty   = false;
    }

    /// <summary>
    /// Reads the live configuration every frame and updates the existing nodes in place.
    /// </summary>
    /// <remarks>
    /// This is the QoLBarEx immediate-mode model: values are pulled, nodes are never rebuilt on data
    /// changes. Pressing a key therefore cannot cause a refresh of this panel.
    /// </remarks>
    protected override void OnDraw() {
        if (_structureDirty) {
            _structureDirty = false;

            BuildBarList();
            BuildContent();
        }

        CustomHotbarConfig? active = Manager.GetConfig(_activeBarId);

        if (active is not null && _hotkeyInputs.Count != active.SlotCount) {
            BuildContent();
        }

        foreach (CustomHotbarConfig config in Manager.All) {
            if (!_barRows.TryGetValue(config.Id, out Node? row)) continue;

            row.ToggleClass("active", config.Id == _activeBarId);
            row.ToggleClass("hidden", !config.IsEnabled);

            if (_lastNames.TryGetValue(config.Id, out string? lastName) && lastName == config.Name) continue;

            _lastNames[config.Id] = config.Name;

            Node? name = row.QuerySelector(".name");
            if (name is not null) name.NodeValue = config.Name;
        }

        if (active is null) return;

        for (int i = 0; i < _hotkeyInputs.Count && i < active.Slots.Count; i++) {
            string label = $"{i + 1}. {DescribeSlot(active.Slots[i])}";

            while (_lastSlotLabels.Count <= i) _lastSlotLabels.Add(string.Empty);

            if (_lastSlotLabels[i] == label) continue;

            _lastSlotLabels[i]     = label;
            _hotkeyInputs[i].Label = label;
        }
    }

    /// <summary>
    /// Called when a bar was added, copied or removed. Only this structural change rebuilds nodes.
    /// </summary>
    private void OnBarsChanged(string? suggestedId) {
        if (suggestedId is not null) {
            _activeBarId = suggestedId;
        }
        else if (Manager.GetConfig(_activeBarId) is null) {
            _activeBarId = Manager.All.FirstOrDefault()?.Id ?? string.Empty;
        }

        _structureDirty = true;
    }

    /// <summary>
    /// Rebuilds the list of bar instances on the left side of the module.
    /// </summary>
    private void BuildBarList() {
        _barList!.ChildNodes = [];
        _barRows.Clear();
        _lastNames.Clear();

        foreach (CustomHotbarConfig config in Manager.All) {
            Node row = BuildBarButton(config);

            _barList.AppendChild(row);
            _barRows[config.Id] = row;
        }
    }

    /// <summary>
    /// Creates the row of one bar instance.
    /// </summary>
    private Node BuildBarButton(CustomHotbarConfig config) {
        var row = new Node { Id = $"bar-{config.Id}" };

        row.ToggleClass("bar-button", true);
        row.ToggleClass("active", config.Id == _activeBarId);
        row.ToggleClass("hidden", !config.IsEnabled);

        var name = new Node { Id = $"name-{config.Id}", NodeValue = config.Name };
        name.ToggleClass("name", true);
        row.AppendChild(name);

        var buttons = new Node();
        buttons.ToggleClass("buttons", true);

        var copyButton = new ButtonNode($"copy-{config.Id}", null, FontAwesomeIcon.Copy, true, true) {
            Tooltip = I18N.Translate("Settings.CustomHotbarModule.CopyBar"),
        };

        copyButton.OnClick += _ => Manager.CreateCopyOfBar(config.Id);
        buttons.AppendChild(copyButton);

        var deleteButton = new ButtonNode($"delete-{config.Id}", null, FontAwesomeIcon.Trash, true, true) {
            Tooltip = I18N.Translate("Settings.CustomHotbarModule.DeleteBar"),
        };

        deleteButton.OnClick += _ => Manager.DeleteBar(config.Id);
        buttons.AppendChild(deleteButton);

        row.AppendChild(buttons);

        row.OnClick += _ => ActivateBar(config.Id);

        _lastNames[config.Id] = config.Name;

        return row;
    }

    /// <summary>
    /// Selects a bar and rebuilds the editor on the right side of the module.
    /// </summary>
    private void ActivateBar(string id) {
        if (_activeBarId == id) return;

        _activeBarId = id;

        BuildContent();
    }

    /// <summary>
    /// Rebuilds the editor of the currently selected bar.
    /// </summary>
    private void BuildContent() {
        _content!.ChildNodes = [];
        _hotkeyInputs.Clear();
        _lastSlotLabels.Clear();

        CustomHotbarConfig? config = Manager.GetConfig(_activeBarId);

        if (config is null) {
            _content.AppendChild(CreateHint(I18N.Translate("Settings.CustomHotbarModule.NoBarSelected")));

            return;
        }

        StringInputNode name = new(
            "ctrl-name",
            config.Name,
            64,
            I18N.Translate("Settings.CustomHotbarModule.Name.Name"),
            I18N.Translate("Settings.CustomHotbarModule.Name.Description"),
            0
        );

        name.OnValueChanged += value => {
            config.Name = value;

            Manager.Persist();
        };

        _content.AppendChild(name);

        CheckboxNode enabled = new(
            "ctrl-enabled",
            config.IsEnabled,
            I18N.Translate("Settings.CustomHotbarModule.Enabled.Name"),
            I18N.Translate("Settings.CustomHotbarModule.Enabled.Description")
        );

        enabled.OnValueChanged += value => {
            config.IsEnabled = value;

            Manager.Persist();
        };

        _content.AppendChild(enabled);

        _content.AppendChild(CreateSeparator(I18N.Translate("Settings.CustomHotbarModule.Layout")));

        IntegerInputNode slotCount = new(
            "ctrl-slot-count",
            config.SlotCount,
            1,
            CustomHotbarAddon.MaxSlots,
            I18N.Translate("Settings.CustomHotbarModule.SlotCount.Name"),
            I18N.Translate("Settings.CustomHotbarModule.SlotCount.Description"),
            0
        );

        slotCount.OnValueChanged += value => {
            config.SlotCount = value;
            config.Normalize();

            Manager.Persist();
        };

        _content.AppendChild(slotCount);

        IntegerInputNode slotsPerRow = new(
            "ctrl-slots-per-row",
            config.SlotsPerRow,
            1,
            CustomHotbarAddon.MaxSlotsPerRow,
            I18N.Translate("Settings.CustomHotbarModule.SlotsPerRow.Name"),
            I18N.Translate("Settings.CustomHotbarModule.SlotsPerRow.Description"),
            0
        );

        slotsPerRow.OnValueChanged += value => {
            config.SlotsPerRow = value;
            config.Normalize();

            Manager.Persist();
        };

        _content.AppendChild(slotsPerRow);

        CheckboxNode locked = new(
            "ctrl-locked",
            config.IsLocked,
            I18N.Translate("Settings.CustomHotbarModule.Locked.Name"),
            I18N.Translate("Settings.CustomHotbarModule.Locked.Description")
        );

        locked.OnValueChanged += value => {
            config.IsLocked = value;
            Manager.Persist();
        };

        _content.AppendChild(locked);

        IntegerInputNode xPos = new(
            "ctrl-x-pos",
            config.XPos,
            -9999,
            9999,
            I18N.Translate("Settings.CustomHotbarModule.XPosition.Name"),
            I18N.Translate("Settings.CustomHotbarModule.XPosition.Description"),
            0
        ) {
            Draggable = true,
        };

        xPos.OnValueChanged += value => {
            config.XPos = value;
            Manager.Persist();
        };

        _content.AppendChild(xPos);

        IntegerInputNode yPos = new(
            "ctrl-y-pos",
            config.YPos,
            -9999,
            9999,
            I18N.Translate("Settings.CustomHotbarModule.YPosition.Name"),
            I18N.Translate("Settings.CustomHotbarModule.YPosition.Description"),
            0
        ) {
            Draggable = true,
        };

        yPos.OnValueChanged += value => {
            config.YPos = value;
            Manager.Persist();
        };

        _content.AppendChild(yPos);

        SelectNode xAlign = new(
            "ctrl-x-align",
            config.XAlign,
            ["Left", "Center", "Right"],
            I18N.Translate("Settings.CustomHotbarModule.XAlign.Name"),
            I18N.Translate("Settings.CustomHotbarModule.XAlign.Description"),
            0
        );

        xAlign.OnValueChanged += value => {
            config.XAlign = value;
            Manager.Persist();
        };

        _content.AppendChild(xAlign);

        SelectNode yAlign = new(
            "ctrl-y-align",
            config.YAlign,
            ["Top", "Center", "Bottom"],
            I18N.Translate("Settings.CustomHotbarModule.YAlign.Name"),
            I18N.Translate("Settings.CustomHotbarModule.YAlign.Description"),
            0
        );

        yAlign.OnValueChanged += value => {
            config.YAlign = value;
            Manager.Persist();
        };

        _content.AppendChild(yAlign);

        _content.AppendChild(CreateSeparator(I18N.Translate("Settings.CustomHotbarModule.Appearance")));

        CheckboxNode showBackground = new(
            "ctrl-show-background",
            config.ShowBackground,
            I18N.Translate("Settings.CustomHotbarModule.ShowBackground.Name"),
            I18N.Translate("Settings.CustomHotbarModule.ShowBackground.Description")
        );

        showBackground.OnValueChanged += value => {
            config.ShowBackground = value;
            Manager.Persist();
        };

        _content.AppendChild(showBackground);

        CheckboxNode autoHide = new(
            "ctrl-auto-hide",
            config.EnableAutoHide,
            I18N.Translate("Settings.CustomHotbarModule.AutoHide.Name"),
            I18N.Translate("Settings.CustomHotbarModule.AutoHide.Description")
        );

        autoHide.OnValueChanged += value => {
            config.EnableAutoHide = value;
            Manager.Persist();
        };

        _content.AppendChild(autoHide);

        CheckboxNode hideEmptySlots = new(
            "ctrl-hide-empty-slots",
            config.HideEmptySlots,
            I18N.Translate("Settings.CustomHotbarModule.HideEmptySlots.Name"),
            I18N.Translate("Settings.CustomHotbarModule.HideEmptySlots.Description")
        );

        hideEmptySlots.OnValueChanged += value => {
            config.HideEmptySlots = value;
            Manager.Persist();
        };

        _content.AppendChild(hideEmptySlots);

        _content.AppendChild(CreateSeparator(I18N.Translate("Settings.CustomHotbarModule.Visibility")));

        _content.AppendChild(
            SelectHelper.Create(
                "custom-hotbar-condition",
                Framework.Service<ConditionSetManager>().GetChoices().Select(entry => (entry.Key, entry.Value)),
                config.ConditionSetId,
                value => {
                    config.ConditionSetId = value;
                    Manager.Persist();
                },
                I18N.Translate("Settings.CustomHotbarModule.Condition.Name"),
                I18N.Translate("Settings.CustomHotbarModule.Condition.Description")
            )
        );

        _content.AppendChild(CreateSeparator(I18N.Translate("Settings.CustomHotbarModule.Hotkeys")));

        _content.AppendChild(CreateHint(I18N.Translate("Settings.CustomHotbarModule.Hotkeys.Hint")));

        string passthroughTooltip = I18N.Translate("Settings.CustomHotbarModule.KeyPassthrough");

        for (int i = 0; i < config.SlotCount; i++) {
            int index = i;

            HotkeyInputNode input = new(
                $"hotkey-{index}",
                Manager.GetSlotHotkey(config.Id, index),
                Manager.GetSlotKeyPassthrough(config.Id, index),
                $"{index + 1}. {DescribeSlot(config.Slots[index])}"
            ) {
                PassthroughTooltip = passthroughTooltip,
            };

            input.OnValueChanged += packed => Manager.SetSlotHotkey(config.Id, index, packed);
            input.OnPassthroughChanged += pass => Manager.SetSlotKeyPassthrough(config.Id, index, pass);

            _content.AppendChild(input);
            _hotkeyInputs.Add(input);
        }
    }

    /// <summary>
    /// Returns a human readable description of the contents of a slot.
    /// </summary>
    private static string DescribeSlot(CustomHotbarSlot slot) {
        switch (slot.Kind) {
            case CustomHotbarSlotKind.Action: {
                var action = Framework.Service<IDataManager>().GetExcelSheet<ActionSheet>().FindRow(slot.Id);

                return action is null
                    ? I18N.Translate("Settings.CustomHotbarModule.UnknownAction")
                    : action.Value.Name.ExtractText();
            }

            case CustomHotbarSlotKind.Item:
            case CustomHotbarSlotKind.Crystal: {
                var item = Framework.Service<IDataManager>().GetExcelSheet<ItemSheet>().FindRow(slot.Id);

                return item is null
                    ? I18N.Translate("Settings.CustomHotbarModule.UnknownItem")
                    : item.Value.Name.ExtractText();
            }

            case CustomHotbarSlotKind.KeyItem: {
                var keyItem = Framework.Service<IDataManager>().GetExcelSheet<KeyItemSheet>().FindRow(slot.Id);

                return keyItem is null
                    ? I18N.Translate("Settings.CustomHotbarModule.UnknownItem")
                    : keyItem.Value.Name.ExtractText();
            }

            case CustomHotbarSlotKind.Macro:
                return I18N.Translate("Settings.CustomHotbarModule.Macro", slot.Id + 1);

            case CustomHotbarSlotKind.Other: {
                // Main commands, general actions, mounts and the like have no dedicated sheet lookup here,
                // so the native type and id are shown instead of pretending the slot is empty.
                var item = Framework.Service<IDataManager>().GetExcelSheet<ItemSheet>().FindRow(slot.Id);

                return item is null
                    ? $"{slot.RawType}#{slot.Id}"
                    : item.Value.Name.ExtractText();
            }

            default:
                return I18N.Translate("Settings.CustomHotbarModule.EmptySlot");
        }
    }

    /// <summary>
    /// Creates a separator with a label.
    /// </summary>
    private static Node CreateSeparator(string label) {
        var node = new Node();

        node.ToggleClass("separator", true);

        var line = new Node();
        line.ToggleClass("line", true);
        node.AppendChild(line);

        var text = new Node { NodeValue = label };
        text.ToggleClass("text", true);
        node.AppendChild(text);

        var trailingLine = new Node();
        trailingLine.ToggleClass("line", true);
        node.AppendChild(trailingLine);

        return node;
    }

    /// <summary>
    /// Creates a small hint text.
    /// </summary>
    private static Node CreateHint(string text) {
        var node = new Node { NodeValue = text };

        node.ToggleClass("hint", true);

        return node;
    }
}
