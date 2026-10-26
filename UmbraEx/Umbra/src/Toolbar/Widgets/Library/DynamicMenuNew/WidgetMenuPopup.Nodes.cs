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

            // Display conditions are evaluated by the widget itself, so the
            // visibility of the entry has to mirror that of the widget.
            row.Style.IsVisible = child?.Node.Style.IsVisible ?? true;

            if (child is SeparatorWidget separator) {
                UpdateSeparatorNode(row, separator);

                continue;
            }

            Node           icon       = row.QuerySelector<Node>(".icon")!;
            Node           text       = row.QuerySelector<Node>(".text")!;
            Node           altText    = row.QuerySelector<Node>(".text-alt")!;
            bool           isDisabled = child?.Node.IsDisabled ?? false;
            string?        alt        = child?.GetMenuAltText();

            text.NodeValue       = child?.GetMenuLabel() ?? GetWidgetTypeName(item.T);
            text.Style.FontSize  = EntryFontSize;
            text.Style.IsVisible = child?.ShowMenuLabel ?? true;
            text.ToggleClass("disabled", isDisabled);

            altText.NodeValue       = alt ?? string.Empty;
            altText.Style.IsVisible = !string.IsNullOrEmpty(alt);
            altText.ToggleClass("disabled", isDisabled);

            icon.ToggleClass("disabled", isDisabled);

            row.Style.Size = new(0, EntryHeight);

            UpdateIconNode(icon, child);
        }
    }

    private Node CreateEntryNode(DynamicMenuNewWidget.WidgetMenuItem item, int index)
    {
        if (WidgetRef?.FindChild(item.G) is SeparatorWidget) {
            return new() {
                ClassList   = ["separator"],
                InheritTags = true,
                SortIndex   = index,
                ChildNodes  = [new() { ClassList = ["separator-line"], InheritTags = true }],
            };
        }

        Node icon = new() { ClassList = ["icon"], InheritTags = true };
        Node wrapper = new() {
            ClassList   = ["icon-wrapper"],
            InheritTags = true,
            Style       = new() { Size = new(EntryHeight, EntryHeight) },
            ChildNodes  = [icon],
        };
        Node text = new() { ClassList = ["text"], InheritTags = true };
        Node alt  = new() { ClassList = ["text-alt"], InheritTags = true, Style = new() { IsVisible = false } };

        Node row = new() {
            ClassList   = ["item"],
            InheritTags = true,
            SortIndex   = index,
            ChildNodes  = [wrapper, text, alt],
        };

        row.OnMouseUp    += _ => ActivateEntry(item);
        row.OnRightClick += _ => ForwardRightClick(item);

        return row;
    }

    private void ActivateEntry(DynamicMenuNewWidget.WidgetMenuItem item)
    {
        ToolbarWidget? child = WidgetRef?.FindChild(item.G);

        if (child is null) return;

        PendingActivation = child;
        Close();
    }

    /// <summary>
    /// Renders a separator widget as a plain horizontal line instead of a menu
    /// entry with an icon and a label.
    /// </summary>
    private static void UpdateSeparatorNode(Node row, SeparatorWidget separator)
    {
        Node line = row.QuerySelector<Node>(".separator-line")!;

        int thickness = Math.Clamp(separator.GetConfigValue<int>("LineWidth"), 1, 8);

        row.Style.Size             = new(0, thickness + 10);
        line.Style.Size            = new(0, thickness);
        line.Style.BackgroundColor = new(separator.GetConfigValue<string>("LineColor"));
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
