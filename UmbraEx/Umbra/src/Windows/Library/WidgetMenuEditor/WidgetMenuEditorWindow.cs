using Dalamud.Interface;
using Umbra.Widgets;
using Umbra.Widgets.Library.DynamicMenuNew;
using Umbra.Widgets.System;

namespace Umbra.Windows.Library.WidgetMenuEditor;

/// <summary>
/// Window in which the widgets that are shown in a "Dynamic Menu (New)" widget
/// are managed.
/// </summary>
internal sealed class WidgetMenuEditorWindow(DynamicMenuNewWidget widget) : Window
{
    protected override string  UdtResourceName => "umbra.windows.widget_menu_editor.window.xml";
    protected override string  Title           => I18N.Translate("Widget.DynamicMenuNew.Editor.Title");
    protected override Vector2 MinSize         => new(420, 220);
    protected override Vector2 MaxSize         => new(720, 720);
    protected override Vector2 DefaultSize     => new(540, 420);

    protected override void OnOpen()
    {
        RootNode.QuerySelector<ButtonNode>("#add-button")!.OnClick   += _ => widget.OpenWidgetBrowser();
        RootNode.QuerySelector<ButtonNode>("#close-button")!.OnClick += _ => Dispose();

        widget.OnItemsChanged += RefreshList;

        RefreshList();
    }

    protected override void OnClose()
    {
        widget.OnItemsChanged -= RefreshList;
    }

    private void RefreshList()
    {
        Node list = RootNode.QuerySelector("#list")!;

        foreach (Node node in list.ChildNodes.ToArray()) {
            list.RemoveChild(node, true);
        }

        if (widget.Items.Count == 0) {
            list.AppendChild(Document!.CreateNodeFromTemplate("empty", new() { { "label", I18N.Translate("Widget.DynamicMenuNew.Editor.Empty") } }));

            return;
        }

        var index = 0;

        foreach (DynamicMenuNewWidget.WidgetMenuItem item in widget.Items) {
            list.AppendChild(CreateEntryNode(item, index++));
        }
    }

    private Node CreateEntryNode(DynamicMenuNewWidget.WidgetMenuItem item, int index)
    {
        ToolbarWidget? child = widget.FindChild(item.G);
        string label = child?.GetInstanceName()
                       ?? Framework.Service<WidgetManager>().GetWidgetInfo(item.T)?.Name
                       ?? item.T;

        Node row     = Document!.CreateNodeFromTemplate("entry", new() { { "label", label } });
        Node buttons = row.QuerySelector(".buttons")!;

        buttons.AppendChild(CreateIconButton("up", FontAwesomeIcon.ArrowUp, "MoveUp", () => widget.MoveItem(item, -1), index == 0));
        buttons.AppendChild(CreateIconButton("down", FontAwesomeIcon.ArrowDown, "MoveDown", () => widget.MoveItem(item, 1), index == widget.Items.Count - 1));
        buttons.AppendChild(CreateIconButton("configure", FontAwesomeIcon.Cog, "Configure", () => widget.OpenChildEditor(item)));
        buttons.AppendChild(CreateIconButton("remove", FontAwesomeIcon.Trash, "Remove", () => widget.RemoveItem(item)));

        return row;
    }

    private static ButtonNode CreateIconButton(
        string id, FontAwesomeIcon icon, string translationKey, Action onClick, bool isDisabled = false
    )
    {
        ButtonNode button = new($"menu_{id}", null, icon, true, true) {
            Tooltip    = I18N.Translate($"Widget.DynamicMenuNew.Editor.{translationKey}"),
            IsDisabled = isDisabled,
        };

        button.OnClick += _ => onClick();

        return button;
    }
}
