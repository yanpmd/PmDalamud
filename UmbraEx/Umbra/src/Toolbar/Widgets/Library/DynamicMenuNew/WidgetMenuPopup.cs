using Umbra.Widgets.Library.DynamicMenuNew;

namespace Umbra.Widgets;

/// <summary>
/// Popup of the "Dynamic Menu (New)" widget. It shows the widgets that were
/// added to the menu and forwards clicks to them.
/// </summary>
internal sealed partial class WidgetMenuPopup : WidgetPopup
{
    internal DynamicMenuNewWidget? WidgetRef { get; set; }

    /// <summary>
    /// Widget that is activated after the menu popup has been closed.
    /// </summary>
    internal ToolbarWidget? PendingActivation { get; set; }

    internal int EntryHeight   { get; set; } = 32;
    internal int EntryFontSize { get; set; } = 13;

    protected override Node Node { get; }

    private readonly UdtDocument _document = UmbraDrawing.DocumentFrom("umbra.widgets.popup_widget_menu.xml");

    private Node ItemList         => Node.QuerySelector<Node>("#ItemList")!;
    private Node EmptyPlaceholder => Node.QuerySelector<Node>("#EmptyPlaceholder")!;

    internal WidgetMenuPopup()
    {
        Node = _document.RootNode!;

        CreateContextMenu();
    }

    protected override void UpdateConfigVariables(ToolbarWidget widget)
    {
        if (widget is not DynamicMenuNewWidget menu) return;

        WidgetRef     = menu;
        EntryHeight   = widget.GetConfigValue<int>("MenuEntryHeight");
        EntryFontSize = widget.GetConfigValue<int>("MenuFontSize");
    }

    protected override void OnOpen()
    {
        RebuildMenu();
    }

    protected override void OnUpdate()
    {
        if (WidgetRef is not { } menu) return;

        // Keep the child widgets up-to-date so their icons and labels reflect
        // the current game state while the menu is open.
        foreach (ToolbarWidget child in menu.Children) {
            child.Update();
        }

        UpdateRowContents();
    }
}
