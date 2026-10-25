using Umbra.Conditions;
using Umbra.Conditions.Builtin;
using Umbra.Windows.Dialogs;

namespace Umbra.Windows.Settings.Modules;

/// <summary>
/// Manages condition sets: which conditions they contain, and their order.
/// Widgets pick a set in their own instance settings.
///
/// Rebuilds are deferred to <see cref="OnDraw"/> because disposing nodes from
/// inside an input callback would pull the rug out from under the node that is
/// currently being drawn.
/// </summary>
[Service]
internal sealed class SettingsWindowConditionsModule : SettingsWindowModule
{
    public override string Name  => I18N.Translate("Settings.ConditionsModule.Name");
    public override int    Order => 30;

    protected override string UdtResourceName => "umbra.windows.settings.modules.conditions_module.xml";

    private ConditionSetManager? _manager;
    private Node?                _setList;
    private Node?                _editor;
    private Node?                _statusNode;
    private Node?                _toolbarBinding;
    private string?              _selectedSetId;

    private bool _rebuildListRequested;
    private bool _rebuildEditorRequested;

    private ConditionSetManager Manager => _manager ??= Framework.Service<ConditionSetManager>();

    private static ConditionRegistry Registry => Framework.Service<ConditionRegistry>();

    protected override void OnOpen()
    {
        _setList        = RootNode.QuerySelector<Node>("#set-list");
        _editor         = RootNode.QuerySelector<Node>("#editor");
        _toolbarBinding = RootNode.QuerySelector<Node>("#toolbar-condition");

        if (RootNode.QuerySelector<ButtonNode>("#add-set") is { } addButton) {
            addButton.OnMouseUp += _ => {
                ConditionSetConfig set = Manager.CreateSet();

                _selectedSetId          = set.Id;
                _rebuildEditorRequested = true;
            };
        }

        _selectedSetId ??= Manager.All.FirstOrDefault()?.Id;

        RebuildSetList();
        RebuildEditor();
    }

    protected override void OnDraw()
    {
        if (_rebuildListRequested) {
            _rebuildListRequested = false;
            RebuildSetList();
        }

        if (_rebuildEditorRequested) {
            _rebuildEditorRequested = false;
            RebuildSetList();
            RebuildEditor();
        }

        if (_statusNode is null) return;

        ConditionSetConfig? set = Manager.Get(_selectedSetId);

        if (set is null) {
            _statusNode.NodeValue = string.Empty;

            return;
        }

        bool active = Manager.IsMatch(set.Id, true);

        _statusNode.NodeValue = I18N.Translate(
            active ? "Settings.ConditionsModule.Result.Pass" : "Settings.ConditionsModule.Result.Fail"
        );
    }

    private void RebuildSetList()
    {
        if (_setList is null) return;

        _setList.ChildNodes = [];

        foreach (ConditionSetConfig set in Manager.All) {
            var row = new Node {
                NodeValue = string.IsNullOrWhiteSpace(set.Name) ? "(unnamed)" : set.Name,
            };

            row.ToggleClass("set-row", true);
            row.ToggleClass("selected", set.Id == _selectedSetId);

            string id = set.Id;

            row.OnMouseUp += _ => {
                _selectedSetId          = id;
                _rebuildEditorRequested = true;
            };

            _setList.AppendChild(row);
        }

        RebuildToolbarBinding();
    }

    /// <summary>
    /// (Re)builds the selector that binds the whole toolbar to a condition set.
    /// Rebuilt together with the set list so that renamed and deleted sets are
    /// reflected in the dropdown.
    /// </summary>
    private void RebuildToolbarBinding()
    {
        if (_toolbarBinding is null) return;

        _toolbarBinding.ChildNodes = [];

        List<(string Id, string Label)> options = Manager.GetChoices().Select(entry => (entry.Key, entry.Value)).ToList();

        SelectNode select = SelectHelper.Create(
            "toolbar-condition-set",
            options,
            Toolbar.VisibilityConditionSet,
            value => ConfigManager.Set("Toolbar.VisibilityConditionSet", value)
        );

        select.LeftMargin = 0;

        _toolbarBinding.AppendChild(select);
    }

    private void RebuildEditor()
    {
        if (_editor is null) return;

        _editor.ChildNodes = [];
        _statusNode        = null;

        ConditionSetConfig? set = Manager.Get(_selectedSetId);

        if (set is null) {
            _editor.AppendChild(CreateHint(I18N.Translate("Settings.ConditionsModule.Empty")));

            return;
        }

        var nameInput = new StringInputNode(
            "set-name",
            set.Name,
            64,
            I18N.Translate("Settings.ConditionsModule.Name.Name"),
            null,
            0
        );

        nameInput.OnValueChanged += value => {
            set.Name = value;
            Manager.Persist();

            // Only the list labels change; rebuilding the editor here would
            // drop focus from this very input.
            _rebuildListRequested = true;
        };

        _editor.AppendChild(nameInput);

        _statusNode = CreateHint(string.Empty);
        _editor.AppendChild(_statusNode);

        var index = 0;

        foreach (ConditionConfig condition in set.Conditions.ToList()) {
            _editor.AppendChild(BuildConditionRow(set, condition, index++));
        }

        _editor.AppendChild(BuildAddConditionButton(set));
        _editor.AppendChild(BuildDeleteSetButton(set));
    }

    private Node BuildConditionRow(ConditionSetConfig set, ConditionConfig condition, int index)
    {
        string suffix = $"-{index}";
        var    row    = new Node();
        var    head   = new Node();

        row.ToggleClass("condition-row", true);
        head.ToggleClass("condition-head", true);
        row.AppendChild(head);

        if (index > 0) {
            List<string> labels = [
                I18N.Translate("Settings.ConditionsModule.Join.And"),
                I18N.Translate("Settings.ConditionsModule.Join.Or"),
                I18N.Translate("Settings.ConditionsModule.Join.Xor"),
            ];

            string current = condition.Join switch {
                ConditionJoin.Or  => labels[1],
                ConditionJoin.Xor => labels[2],
                _                 => labels[0],
            };

            var joinNode = new SelectNode($"join{suffix}", current, labels, null, null, 0);

            joinNode.OnValueChanged += value => {
                condition.Join = value == labels[1]
                    ? ConditionJoin.Or
                    : value == labels[2]
                        ? ConditionJoin.Xor
                        : ConditionJoin.And;

                Manager.Persist();
            };

            head.AppendChild(joinNode);
        }

        var negateNode = new CheckboxNode($"negate{suffix}", condition.Negate, I18N.Translate("Settings.ConditionsModule.Negate"));

        negateNode.OnValueChanged += value => {
            condition.Negate = value;
            Manager.Persist();
        };

        head.AppendChild(negateNode);

        var options = new List<(string Id, string Label)>();

        foreach (ConditionCategoryGroup group in Registry.Categories) {
            foreach (IDisplayCondition item in group.Items) {
                options.Add((item.Id, item.Name));
            }
        }

        head.AppendChild(
            SelectHelper.Create(
                $"ctype{suffix}",
                options,
                condition.Id,
                value => {
                    if (condition.Id == value) return;

                    condition.Id  = value;
                    condition.Arg = new();
                    Manager.Persist();

                    _rebuildEditorRequested = true;
                }
            )
        );

        var removeButton = new ButtonNode($"remove-condition{suffix}", null, FontAwesomeIcon.Trash, true, true);

        removeButton.OnMouseUp += _ => {
            Confirm(
                I18N.Translate("Settings.ConditionsModule.DeleteConditionConfirmation.Title"),
                I18N.Translate(
                    "Settings.ConditionsModule.DeleteConditionConfirmation.Message",
                    Registry.Get(condition.Id)?.Name ?? condition.Id,
                    string.IsNullOrWhiteSpace(set.Name) ? I18N.Translate("Settings.ConditionsModule.Unnamed") : set.Name
                ),
                () => {
                    set.Conditions.Remove(condition);
                    Manager.Persist();

                    _rebuildEditorRequested = true;
                }
            );
        };

        head.AppendChild(removeButton);

        var parameters = new Node();

        parameters.ToggleClass("condition-params", true);
        row.AppendChild(parameters);

        Registry.Get(condition.Id)?.BuildEditor(parameters, condition.Arg, suffix, () => Manager.Persist());

        return row;
    }

    private Node BuildAddConditionButton(ConditionSetConfig set)
    {
        var button = new ButtonNode(
            "add-condition",
            I18N.Translate("Settings.ConditionsModule.AddCondition"),
            FontAwesomeIcon.Plus,
            true,
            true
        );

        button.OnMouseUp += _ => {
            set.Conditions.Add(
                new ConditionConfig {
                    Id  = Registry.Default?.Id ?? "cf",
                    Arg = new(),
                }
            );

            Manager.Persist();

            _rebuildEditorRequested = true;
        };

        return button;
    }

    private Node BuildDeleteSetButton(ConditionSetConfig set)
    {
        var button = new ButtonNode(
            "delete-set",
            I18N.Translate("Settings.ConditionsModule.DeleteSet"),
            FontAwesomeIcon.Trash,
            true,
            true
        );

        button.OnMouseUp += _ => {
            Confirm(
                I18N.Translate("Settings.ConditionsModule.DeleteSetConfirmation.Title"),
                I18N.Translate(
                    "Settings.ConditionsModule.DeleteSetConfirmation.Message",
                    string.IsNullOrWhiteSpace(set.Name) ? I18N.Translate("Settings.ConditionsModule.Unnamed") : set.Name
                ),
                () => {
                    Manager.DeleteSet(set.Id);
                    _selectedSetId = Manager.All.FirstOrDefault()?.Id;

                    _rebuildEditorRequested = true;
                },
                I18N.Translate("Settings.ConditionsModule.DeleteSetConfirmation.Hint")
            );
        };

        return button;
    }

    /// <summary>
    /// Asks the user to confirm a destructive action. The callback only runs
    /// when the confirmation window was accepted.
    /// </summary>
    private static void Confirm(string title, string message, Action onConfirm, string hint = "")
    {
        Framework.Service<WindowManager>().Present<ConfirmationWindow>(
            "SettingsConditionsConfirmation",
            new(
                title,
                message,
                I18N.Translate("Delete"),
                I18N.Translate("Cancel"),
                hint
            ),
            window => {
                if (window.Confirmed) onConfirm();
            }
        );
    }

    private static Node CreateHint(string text)
    {
        var node = new Node { NodeValue = text };

        node.ToggleClass("hint", true);

        return node;
    }
}
