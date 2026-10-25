using Umbra.MoreControls.Conditions;
using Umbra.MoreControls.Runtime;

namespace Umbra.MoreControls.UI;

/// <summary>
/// Configuration window for condition sets and their bindings.
///
/// Umbra's settings window is sealed inside the main assembly, so plugins get
/// their own window that is drawn during the ImGui draw pass.
/// </summary>
internal static class ConditionWindow
{
    private const string WindowTitle = "Umbra 显示条件###UmbraMoreControls";

    private static readonly Vector4 ColorPositive = new(0.45f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 ColorNegative = new(0.90f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 ColorMuted    = new(1f, 1f, 1f, 0.5f);

    private static bool    _isOpen;
    private static bool    _requestSetsTab;
    private static string? _selectedSetId;

    public static void Toggle()
    {
        _isOpen = !_isOpen;
    }

    public static void Show()
    {
        _isOpen = true;
    }

    public static void Draw()
    {
        if (!_isOpen) return;

        Vector2 viewportSize = ImGui.GetMainViewport().WorkSize;
        Vector2 viewportPos  = ImGui.GetMainViewport().WorkPos;

        ImGui.SetNextWindowPos(
            viewportPos + (viewportSize / 2f),
            ImGuiCond.FirstUseEver,
            new Vector2(0.5f, 0.5f)
        );

        ImGui.SetNextWindowSize(new Vector2(900, 580), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin(WindowTitle, ImGuiWindowFlags.NoCollapse)) {
            ImGui.End();
            return;
        }

        DrawHeader();

        if (ImGui.BeginTabBar("##morecontrols-tabs")) {
            ImGuiTabItemFlags setsFlags = _requestSetsTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;

            if (ImGui.BeginTabItem("条件组", setsFlags)) {
                DrawSetsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("绑定")) {
                DrawBindingsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        _requestSetsTab = false;

        ImGui.End();
    }

    private static void DrawHeader()
    {
        ImGui.TextDisabled("为 Umbra 的工具栏、面板和小组件设置显示条件（类似 QoLBar 的条件组）。");

        ImGui.SameLine();

        float buttonWidth = 70f;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - buttonWidth - 16f);

        if (ImGui.Button("关闭", new Vector2(buttonWidth, 0))) {
            _isOpen = false;
        }

        ImGui.Separator();
    }

    // ---------------------------------------------------------------- sets

    private static void DrawSetsTab()
    {
        MoreControlsConfig config = ConfigStore.Config;

        if (_selectedSetId is not null && config.FindSet(_selectedSetId) is null) {
            _selectedSetId = null;
        }

        // Always have something selected so the editor is never an empty pane.
        if (_selectedSetId is null && config.Sets.Count > 0) {
            _selectedSetId = config.Sets[0].Id;
        }

        ImGui.BeginChild("##set-list", new Vector2(230, -34), true);

        foreach (ConditionSetConfig set in config.Sets.ToList()) {
            bool   isActive = ConditionManager.EvaluateSet(set);
            string label    = set.Name.Length == 0 ? "（未命名）" : set.Name;

            ImGui.PushStyleColor(ImGuiCol.Text, isActive ? ColorPositive : ColorNegative);

            if (ImGui.Selectable($"{label}##{set.Id}", set.Id == _selectedSetId)) {
                _selectedSetId = set.Id;
            }

            ImGui.PopStyleColor();
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##set-editor", new Vector2(0, -34), true);

        ConditionSetConfig? selected = config.FindSet(_selectedSetId);

        if (selected is null) {
            ImGui.TextDisabled("还没有条件组。点击下方的「新建条件组」开始。");

            if (ImGui.Button("新建条件组##empty")) {
                ConditionSetConfig created = config.AddSet($"条件组 {config.Sets.Count + 1}");
                _selectedSetId = created.Id;
                ConfigStore.MarkDirty();
            }
        } else {
            DrawSetEditor(selected);
        }

        ImGui.EndChild();

        if (ImGui.Button("新建条件组")) {
            ConditionSetConfig set = config.AddSet($"条件组 {config.Sets.Count + 1}");
            _selectedSetId = set.Id;
            ConfigStore.MarkDirty();
        }

        ImGui.SameLine();

        ImGui.BeginDisabled(selected is null);

        if (ImGui.Button("复制")) {
            var copy = new ConditionSetConfig {
                Name = $"{selected!.Name} 副本",
                Conditions =
                    selected.Conditions
                            .Select(c => new ConditionConfig {
                                Id     = c.Id,
                                Negate = c.Negate,
                                Join   = c.Join,
                                Arg    = c.Arg.Clone(),
                            })
                            .ToList(),
            };

            config.Sets.Add(copy);
            _selectedSetId = copy.Id;
            ConfigStore.MarkDirty();
        }

        ImGui.SameLine();

        if (ImGui.Button("删除")) {
            config.RemoveSet(selected!);
            _selectedSetId = config.Sets.FirstOrDefault()?.Id;
            ConfigStore.MarkDirty();
        }

        ImGui.EndDisabled();
    }

    private static void DrawSetEditor(ConditionSetConfig set)
    {
        string name = set.Name;

        ImGui.SetNextItemWidth(260);

        if (ImGui.InputText("名称", ref name, 64)) {
            set.Name = name;
            ConfigStore.MarkDirty();
        }

        ImGui.SameLine();

        bool active = ConditionManager.EvaluateSet(set);

        ImGui.TextColored(active ? ColorPositive : ColorNegative, active ? "当前结果：通过" : "当前结果：不通过");

        ImGui.TextDisabled("条件按自上而下的顺序求值；第一行的运算符会被忽略。");
        ImGui.Separator();

        int removeIndex = -1;

        for (var i = 0; i < set.Conditions.Count; i++) {
            ConditionConfig cnd = set.Conditions[i];

            ImGui.PushID(i);
            DrawConditionRow(cnd, i);

            if (ImGui.SmallButton("删除条件")) removeIndex = i;

            ImGui.Separator();
            ImGui.PopID();
        }

        if (removeIndex >= 0) {
            set.Conditions.RemoveAt(removeIndex);
            ConfigStore.MarkDirty();
        }

        if (ImGui.Button("+ 添加条件")) {
            IDisplayCondition? defaults = ConditionManager.GetDefault();

            set.Conditions.Add(
                new ConditionConfig {
                    Id  = defaults?.Id ?? "cf",
                    Arg = new(),
                }
            );

            ConfigStore.MarkDirty();
        }
    }

    private static void DrawConditionRow(ConditionConfig cnd, int index)
    {
        // Row 1: operator, negation, condition type.
        if (index == 0) {
            ImGui.TextDisabled("     ");
        } else {
            ImGui.SetNextItemWidth(90);
            DrawJoinCombo(cnd);
        }

        ImGui.SameLine();

        bool negate = cnd.Negate;

        if (ImGui.Checkbox("非", ref negate)) {
            cnd.Negate = negate;
            ConfigStore.MarkDirty();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(280);
        DrawConditionTypeCombo(cnd);

        IDisplayCondition? condition = ConditionManager.Get(cnd.Id);
        bool        value     = ConditionManager.CheckOne(cnd);

        ImGui.SameLine();
        ImGui.TextColored(value ? ColorPositive : ColorNegative, value ? "✔" : "✘");

        // Row 2: argument editor.
        if (condition is not null) {
            ImGui.Indent(24);

            try {
                condition.DrawEditor(cnd.Arg);
            } catch (Exception e) {
                ImGui.TextColored(ColorNegative, $"参数编辑器异常：{e.Message}");
            }

            ImGui.Unindent(24);
        }
    }

    private static void DrawJoinCombo(ConditionConfig cnd)
    {
        string label = cnd.Join switch {
            ConditionJoin.Or  => "或者",
            ConditionJoin.Xor => "异或",
            _                 => "并且",
        };

        if (!ImGui.BeginCombo("##join", label)) return;

        foreach (ConditionJoin join in Enum.GetValues<ConditionJoin>()) {
            string text = join switch {
                ConditionJoin.Or  => "或者",
                ConditionJoin.Xor => "异或",
                _                 => "并且",
            };

            if (ImGui.Selectable(text, join == cnd.Join)) {
                cnd.Join = join;
                ConfigStore.MarkDirty();
            }
        }

        ImGui.EndCombo();
    }

    private static void DrawConditionTypeCombo(ConditionConfig cnd)
    {
        IDisplayCondition? current = ConditionManager.Get(cnd.Id);

        if (!ImGui.BeginCombo("##ctype", current?.Name ?? cnd.Id)) return;

        foreach (ConditionCategoryGroup group in ConditionManager.Categories) {
            ImGui.TextDisabled(group.Name);

            foreach (IDisplayCondition cond in group.Items) {
                if (!ImGui.Selectable($"{cond.Name}##{cond.Id}", cond.Id == cnd.Id)) continue;

                if (cnd.Id != cond.Id) {
                    cnd.Id  = cond.Id;
                    cnd.Arg = new ConditionArg();
                    ConfigStore.MarkDirty();
                }
            }
        }

        ImGui.EndCombo();
    }

    // ------------------------------------------------------------- bindings

    private static void DrawBindingsTab()
    {
        MoreControlsConfig config = ConfigStore.Config;

        if (ImGui.Button("＋ 新建条件组")) {
            ConditionSetConfig created = config.AddSet($"条件组 {config.Sets.Count + 1}");
            _selectedSetId = created.Id;
            _requestSetsTab = true;
            ConfigStore.MarkDirty();
        }

        ImGui.SameLine();
        ImGui.TextDisabled(
            config.Sets.Count == 0
                ? "还没有条件组；先创建一个，再到「条件组」标签页编辑具体条件。"
                : "未绑定的目标始终显示。绑定后仅当条件组成立时才显示。"
        );

        ImGui.Separator();

        if (!UmbraBridge.IsResolved) {
            ImGui.TextColored(ColorNegative, "无法访问 Umbra 内部接口，无法控制小组件显示（可能是 Umbra 版本不兼容）。");
            ImGui.Separator();
        }

        ImGui.Text("工具栏与面板");

        foreach (string id in MoreControlsPlugin.PanelIds) {
            string label = id switch {
                "Toolbar" => "工具栏（整体）",
                "Left"    => "左侧面板",
                "Center"  => "中央面板",
                "Right"   => "右侧面板",
                _         => id,
            };

            DrawBindingRow(config.Panels, id, label, () => UmbraBridge.SetPanelVisible(id, true));
        }

        ImGui.Separator();
        ImGui.Text("小组件");

        List<ToolbarWidget> widgets = UmbraBridge.GetWidgets();

        if (widgets.Count == 0) {
            ImGui.TextDisabled("未检测到小组件（请确认 Umbra 已完全加载）。");
        }

        foreach (ToolbarWidget widget in widgets) {
            string id       = widget.Id;
            string name     = widget.GetInstanceName();
            string location = widget.Location switch {
                "Left"   => "左",
                "Center" => "中",
                "Right"  => "右",
                _        => widget.Location,
            };

            string label = $"{Shorten($"[{location}] {name}", 30)} · {id[..Math.Min(4, id.Length)]}";
            string key   = id;

            DrawBindingRow(config.Widgets, key, label, () => UmbraBridge.SetWidgetVisible(widget, true));
        }

        ImGui.Separator();
        DrawOrphanBindings(config, widgets);
    }

    private static void DrawBindingRow(
        Dictionary<string, ConditionBinding> bindings,
        string                               key,
        string                               label,
        Action                               restoreVisibility
    )
    {
        ImGui.PushID(key);

        bindings.TryGetValue(key, out ConditionBinding? binding);

        var    setIndex = 0;
        string preview  = "无条件（始终显示）";

        var options = new List<string> { "无条件（始终显示）" };
        options.AddRange(ConfigStore.Config.Sets.Select(s => s.Name.Length == 0 ? "（未命名）" : s.Name));

        if (binding is not null && binding.IsBound) {
            int found = ConfigStore.Config.Sets.FindIndex(s => s.Id == binding.SetId);
            setIndex = found >= 0 ? found + 1 : 0;
            preview  = options[setIndex];
        }

        ImGui.TextUnformatted(label);

        ImGui.SameLine(260);
        ImGui.SetNextItemWidth(220);

        if (ImGui.BeginCombo("##binding", preview)) {
            for (var i = 0; i < options.Count; i++) {
                if (!ImGui.Selectable($"{options[i]}##opt{i}", i == setIndex)) continue;

                if (i == 0) {
                    bindings.Remove(key);
                    restoreVisibility();
                } else {
                    ConditionSetConfig set = ConfigStore.Config.Sets[i - 1];

                    bindings[key] = new ConditionBinding {
                        SetId  = set.Id,
                        Invert = binding?.Invert ?? false,
                    };
                }

                ConfigStore.MarkDirty();
            }

            ImGui.EndCombo();
        }

        if (binding is not null && binding.IsBound) {
            ImGui.SameLine();

            bool invert = binding.Invert;

            if (ImGui.Checkbox("反选", ref invert)) {
                binding.Invert = invert;
                ConfigStore.MarkDirty();
            }

            ImGui.SameLine();

            bool visible = ConditionManager.EvaluateBinding(binding);

            ImGui.TextColored(visible ? ColorPositive : ColorNegative, visible ? "显示中" : "已隐藏");
        }

        ImGui.PopID();
    }

    private static string Shorten(string text, int max)
    {
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    private static void DrawOrphanBindings(MoreControlsConfig config, List<ToolbarWidget> widgets)
    {
        var alive = widgets.Select(w => w.Id).ToHashSet();

        List<string> orphans = config.Widgets.Keys.Where(id => !alive.Contains(id)).ToList();

        if (orphans.Count == 0) return;

        ImGui.TextColored(ColorMuted, $"存在 {orphans.Count} 条已失效的小组件绑定（对应的小组件已不存在）。");

        if (!ImGui.Button("清理失效绑定")) return;

        foreach (string id in orphans) config.Widgets.Remove(id);

        ConfigStore.MarkDirty();
    }
}
