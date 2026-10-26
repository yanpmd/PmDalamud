using Umbra.Widgets.Library.DynamicMenuNew;

namespace Umbra.Widgets;

internal sealed partial class WidgetMenuPopup
{
    private DynamicMenuNewWidget.WidgetMenuItem? _selectedItem;

    private void CreateContextMenu()
    {
        ContextMenu = new([
            new ContextMenuEntry("Configure") {
                Label   = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.Configure"),
                OnClick = () => {
                    if (_selectedItem is null) return;

                    DynamicMenuNewWidget.WidgetMenuItem item = _selectedItem;
                    DynamicMenuNewWidget?              menu = WidgetRef;

                    Close();
                    menu?.OpenChildEditor(item);
                },
            },
            new ContextMenuEntry("-"),
            new ContextMenuEntry("MoveUp") {
                Label   = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.MoveUp"),
                OnClick = () => MoveSelected(item => WidgetRef?.MoveItem(item, -1)),
            },
            new ContextMenuEntry("MoveDown") {
                Label   = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.MoveDown"),
                OnClick = () => MoveSelected(item => WidgetRef?.MoveItem(item, 1)),
            },
            new ContextMenuEntry("MoveToTop") {
                Label   = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.MoveToTop"),
                OnClick = () => MoveSelected(item => WidgetRef?.MoveItemToEdge(item, false)),
            },
            new ContextMenuEntry("MoveToBottom") {
                Label   = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.MoveToBottom"),
                OnClick = () => MoveSelected(item => WidgetRef?.MoveItemToEdge(item, true)),
            },
            new ContextMenuEntry("-"),
            new ContextMenuEntry("Manage") {
                Label   = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.Manage"),
                OnClick = () => {
                    Close();
                    WidgetRef?.OpenContentEditor();
                },
            },
            new ContextMenuEntry("Remove") {
                Label   = I18N.Translate("Widget.DynamicMenuNew.ContextMenu.Remove"),
                OnClick = () => MoveSelected(item => WidgetRef?.RemoveItem(item)),
            },
        ]);
    }

    private void OpenContextMenuFor(DynamicMenuNewWidget.WidgetMenuItem item)
    {
        _selectedItem = item;

        ContextMenu?.Present();
    }

    private void MoveSelected(Action<DynamicMenuNewWidget.WidgetMenuItem> action)
    {
        if (_selectedItem is null) return;

        DynamicMenuNewWidget.WidgetMenuItem item = _selectedItem;

        Close();
        action(item);
    }
}
