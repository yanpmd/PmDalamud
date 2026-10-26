using Umbra.Widgets.Library.DynamicMenuNew;

namespace Umbra.Widgets;

internal sealed partial class WidgetMenuPopup
{
    private void CreateContextMenu()
    {
        ContextMenu = new([
            new ContextMenuEntry("Configure") {
                Label = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.Configure"),
                OnClick = () => {
                    DynamicMenuNewWidget? menu = WidgetRef;

                    Close();
                    menu?.OpenSettingsWindow();
                },
            },
            new ContextMenuEntry("Manage") {
                Label = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.Manage"),
                OnClick = () => {
                    DynamicMenuNewWidget? menu = WidgetRef;

                    Close();
                    menu?.OpenContentEditor();
                },
            },
        ]);
    }

    /// <summary>
    /// Shows the context menu of the menu widget itself, which is presented
    /// when the button of the menu is right-clicked.
    /// </summary>
    internal void PresentWidgetContextMenu()
    {
        ContextMenu?.Present();
    }

    /// <summary>
    /// Forwards a right click on a menu entry to the widget it represents so
    /// that entries behave like their standalone counterparts.
    /// </summary>
    private void ForwardRightClick(DynamicMenuNewWidget.WidgetMenuItem item)
    {
        ToolbarWidget? child = WidgetRef?.FindChild(item.G);

        if (child is null) return;
        if (!DynamicMenuNewWidget.HasNodeMouseHandler(child.Node, "OnRightClick")) return;

        Close();

        Framework.DalamudFramework.Run(() => DynamicMenuNewWidget.InvokeNodeMouseEvent(child.Node, "OnRightClick"));
    }
}
