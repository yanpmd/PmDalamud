using Dalamud.Game.Command;
using Umbra.MoreControls.Conditions;
using Umbra.MoreControls.Runtime;
using Umbra.MoreControls.UI;

namespace Umbra.MoreControls;

/// <summary>
/// Plugin entry point. Created by Umbra's service container because the loaded
/// assembly is registered with <c>Framework.Assemblies</c>.
/// </summary>
[Service]
internal sealed class MoreControlsPlugin : IDisposable
{
    private const string CommandName = "/morecontrols";

    /// <summary>Targets that can carry a condition binding.</summary>
    public static readonly string[] PanelIds = ["Toolbar", "Left", "Center", "Right"];

    private ICommandManager? _commands;
    private bool             _diagnosed;
    private bool             _pendingToolbarButton;

    public MoreControlsPlugin()
    {
        bool isFirstRun = ConfigStore.Initialize();
        ConditionManager.Initialize();

        try {
            _commands = Framework.Service<ICommandManager>();
            _commands.AddHandler(
                CommandName,
                new CommandInfo(OnCommand) {
                    HelpMessage = "打开 Umbra 显示条件设置窗口（MoreControls）。",
                }
            );
        } catch (Exception e) {
            Logger.Warning($"[MoreControls] Could not register {CommandName}: {e.Message}");
        }

        Logger.Info($"[MoreControls] Loaded. Use {CommandName} to configure visibility conditions.");

        // Open the window once on a fresh install so the plugin is visibly doing
        // something, and put a hint in the chat log either way.
        if (isFirstRun) {
            ConditionWindow.Show();
            _pendingToolbarButton = true;
        }

        try {
            Framework.Service<IChatGui>().Print(
                isFirstRun
                    ? "MoreControls 已加载：显示条件窗口已打开。以后用 /morecontrols 打开，或在 Umbra「添加小组件」中搜索「显示条件」。"
                    : "MoreControls 已加载。使用 /morecontrols 打开显示条件设置。"
            );
        } catch (Exception e) {
            Logger.Warning($"[MoreControls] Could not print chat message: {e.Message}");
        }
    }

    public void Dispose()
    {
        // Make sure we never leave Umbra's toolbar hidden after being unloaded.
        foreach (ToolbarWidget widget in UmbraBridge.GetWidgets()) {
            UmbraBridge.SetWidgetVisible(widget, true);
        }

        foreach (string id in PanelIds) {
            UmbraBridge.SetPanelVisible(id, true);
        }

        ConfigStore.Save();

        if (_commands is not null) {
            _commands.RemoveHandler(CommandName);
            _commands = null;
        }
    }

    private static void OnCommand(string command, string args)
    {
        ConditionWindow.Toggle();
    }

    /// <summary>
    /// Applies all condition bindings. Runs after Umbra's own widget update, so
    /// writing the node style here takes effect on the same frame.
    /// </summary>
    [OnTick(interval: 16)]
    private void ApplyVisibility()
    {
        ConfigStore.Flush();

        if (!_diagnosed) {
            _diagnosed = true;

            List<ToolbarWidget> widgets = UmbraBridge.GetWidgets();

            Logger.Info(
                $"[MoreControls] 诊断：内部接口={(UmbraBridge.IsResolved ? "已解析" : "未解析")}，"
                + $"小组件实例={widgets.Count}，条件={ConditionManager.Categories.Sum(g => g.Items.Count)} 种。"
            );

            if (_pendingToolbarButton) {
                _pendingToolbarButton = false;
                EnsureToolbarButton(widgets);
            }
        }

        MoreControlsConfig config = ConfigStore.Config;

        if (config.Widgets.Count == 0 && config.Panels.Count == 0) return;

        foreach (ToolbarWidget widget in UmbraBridge.GetWidgets()) {
            ConditionBinding? binding = config.Widgets.GetValueOrDefault(widget.Id);

            if (binding is null) continue;

            UmbraBridge.SetWidgetVisible(widget, ConditionManager.EvaluateBinding(binding));
        }

        foreach (string id in PanelIds) {
            ConditionBinding? binding = config.Panels.GetValueOrDefault(id);

            if (binding is null) continue;

            UmbraBridge.SetPanelVisible(id, ConditionManager.EvaluateBinding(binding));
        }
    }

    /// <summary>
    /// Adds the toolbar button on a fresh install so the plugin has a visible
    /// entry point. Skipped when an instance already exists.
    /// </summary>
    private static void EnsureToolbarButton(List<ToolbarWidget> widgets)
    {
        if (widgets.Any(w => w.Info.Id == "MoreControls")) return;

        if (!UmbraBridge.CreateWidget("MoreControls", "Right")) return;

        try {
            Framework.Service<IChatGui>().Print("MoreControls：已在工具栏右侧添加「条件」按钮。");
        } catch {
            // Chat output is optional.
        }
    }

    [OnDraw]
    private void DrawConfigWindow()
    {
        ConditionWindow.Draw();
    }
}
