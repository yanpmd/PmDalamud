using Dalamud.Interface;
using Umbra.Widgets.Library.DynamicMenuNew;
using Umbra.Widgets.System;

namespace Umbra.Widgets;

internal sealed partial class WidgetMenuPopup
{
    private readonly Dictionary<string, Node> _rows = [];

    private void RebuildMenu()
    {
        foreach (Node node in ItemList.ChildNodes.ToArray()) {
            ItemList.RemoveChild(node, true);
        }

        _rows.Clear();

        if (WidgetRef is not { } menu) return;

        var index = 0;

        foreach (DynamicMenuNewWidget.WidgetMenuItem item in menu.Items) {
            Node row = CreateEntryNode(item, index++);

            ItemList.AppendChild(row);
            _rows[item.G] = row;
        }

        EmptyPlaceholder.NodeValue      = I18N.Translate("Widget.DynamicMenuNew.EmptyPlaceholder");
        EmptyPlaceholder.Style.IsVisible = menu.Items.Count == 0;

        UpdateRowContents();
    }

    private void UpdateRowContents()
    {
        if (WidgetRef is not { } menu) return;

        foreach (DynamicMenuNewWidget.WidgetMenuItem item in menu.Items) {
            if (!_rows.TryGetValue(item.G, out Node? row)) continue;

            ToolbarWidget? child = menu.FindChild(item.G);
            Node           icon  = row.QuerySelector<Node>(".icon")!;
            Node           text  = row.QuerySelector<Node>(".text")!;

            text.NodeValue      = child?.GetInstanceName() ?? GetWidgetTypeName(item.T);
            text.Style.FontSize = EntryFontSize;
            row.Style.Size      = new(0, EntryHeight);

            UpdateIconNode(icon, child);
        }
    }

    private Node CreateEntryNode(DynamicMenuNewWidget.WidgetMenuItem item, int index)
    {
        Node icon = new() { ClassList = ["icon"], InheritTags = true };
        Node wrapper = new() {
            ClassList   = ["icon-wrapper"],
            InheritTags = true,
            Style       = new() { Size = new(EntryHeight, EntryHeight) },
            ChildNodes  = [icon],
        };
        Node text = new() { ClassList = ["text"], InheritTags = true };

        Node row = new() {
            ClassList   = ["item"],
            InheritTags = true,
            SortIndex   = index,
            ChildNodes  = [wrapper, text],
        };

        row.OnMouseUp    += _ => ActivateEntry(item);
        row.OnRightClick += _ => OpenContextMenuFor(item);

        return row;
    }

    private void ActivateEntry(DynamicMenuNewWidget.WidgetMenuItem item)
    {
        ToolbarWidget? child = WidgetRef?.FindChild(item.G);

        if (child is null) return;

        PendingActivation = child;
        Close();
    }

    private static string GetWidgetTypeName(string typeId)
    {
        WidgetInfo? info = Framework.Service<WidgetManager>().GetWidgetInfo(typeId);

        return info?.Name ?? typeId;
    }

    private void UpdateIconNode(Node icon, ToolbarWidget? child)
    {
        Node? source = child?.Node.QuerySelector(".icon");

        if (source is null) {
            icon.ToggleClass("fa-icon", true);
            icon.ToggleClass("glyph-icon", false);
            icon.ToggleClass("game-icon", false);
            icon.ToggleClass("uld", false);

            icon.NodeValue            = FontAwesomeIcon.Bars.ToIconString();
            icon.Style.IconId         = null;
            icon.Style.BitmapFontIcon = null;
            icon.Style.Size           = new(EntryHeight - 5, EntryHeight - 5);
            icon.Style.FontSize       = Math.Max(6, EntryHeight - 16);

            return;
        }

        bool isTextGlyph = source.ClassList.Contains("fa-icon") || source.ClassList.Contains("glyph-icon");

        icon.ToggleClass("fa-icon", source.ClassList.Contains("fa-icon"));
        icon.ToggleClass("glyph-icon", source.ClassList.Contains("glyph-icon"));
        icon.ToggleClass("game-icon", source.ClassList.Contains("game-icon"));
        icon.ToggleClass("uld", source.ClassList.Contains("uld"));

        icon.NodeValue            = source.NodeValue;
        icon.Style.IconId         = source.Style.IconId;
        icon.Style.BitmapFontIcon = source.Style.BitmapFontIcon;

        if (!ReferenceEquals(icon.Style.ImageBytes, source.Style.ImageBytes)) {
            icon.Style.ImageBytes = source.Style.ImageBytes;
        }

        icon.Style.UldPartId      = source.Style.UldPartId;
        icon.Style.UldPartsId     = source.Style.UldPartsId;
        icon.Style.UldResource    = source.Style.UldResource;
        icon.Style.ImageGrayscale = source.Style.ImageGrayscale;

        icon.Style.Size = new(EntryHeight - 5, EntryHeight - 5);

        if (isTextGlyph) {
            icon.Style.FontSize = Math.Max(6, EntryHeight - 16);
        } else {
            icon.Style.ImageColor = source.Style.ImageColor;
        }
    }
}
