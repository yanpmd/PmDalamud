using Umbra.MoreControls.UI;

namespace Umbra.MoreControls.Widgets;

/// <summary>
/// Toolbar button that opens the condition window. This gives users an in-game
/// entry point without having to remember the slash command.
/// </summary>
[ToolbarWidget("MoreControls", "显示条件", "打开 Umbra 显示条件设置窗口。")]
public sealed class MoreControlsWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Icon | StandardWidgetFeatures.Text;

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return base.GetConfigVariables();
    }

    protected override void OnLoad()
    {
        SetText("条件");
        SetGameIconId(14);

        Node.OnMouseDown += _ => {
            if (Node.IsDisabled) return;

            ConditionWindow.Toggle();
        };
    }

    protected override void OnUnload()
    {
    }
}
