using Newtonsoft.Json;

using Umbra.Common.Utility;
using Umbra.Windows;
using Umbra.Windows.Library.WidgetBrowser;
using Umbra.Windows.Library.WidgetMenuEditor;
using Umbra.Widgets.System;

namespace Umbra.Widgets.Library.DynamicMenuNew;

internal sealed partial class DynamicMenuNewWidget
{
    /// <summary>
    /// A widget that is shown in this menu.
    /// </summary>
    [Serializable]
    internal sealed class WidgetMenuItem
    {
        /// <summary>Unique id of this menu entry.</summary>
        public string G { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Id of the widget type.</summary>
        public string T { get; set; } = "";

        /// <summary>Configuration values of the widget.</summary>
        public Dictionary<string, object>? C { get; set; }
    }

    private readonly List<WidgetMenuItem> _items    = [];
    private readonly List<ToolbarWidget> _children  = [];
    private bool                         _isSyncingItems;

    internal IReadOnlyList<WidgetMenuItem> Items    => _items;
    internal IReadOnlyList<ToolbarWidget>  Children => _children;

    internal event Action? OnItemsChanged;

    internal ToolbarWidget? FindChild(string itemId)
    {
        return _children.FirstOrDefault(child => child.Id == itemId);
    }

    internal void AddItem(string widgetTypeId)
    {
        // Prevent adding the menu to itself.
        if (widgetTypeId == Info.Id) return;

        _items.Add(new() { T = widgetTypeId });

        SaveItems();
        RebuildChildren();
        OnItemsChanged?.Invoke();
    }

    internal void RemoveItem(WidgetMenuItem item)
    {
        if (!_items.Remove(item)) return;

        SaveItems();
        RebuildChildren();
        OnItemsChanged?.Invoke();
    }

    internal void MoveItem(WidgetMenuItem item, int delta)
    {
        int index = _items.IndexOf(item);
        if (index < 0) return;

        int target = Math.Clamp(index + delta, 0, _items.Count - 1);
        if (target == index) return;

        _items.RemoveAt(index);
        _items.Insert(target, item);

        SaveItems();
        ReorderChildren();
        OnItemsChanged?.Invoke();
    }

    internal void MoveItemToEdge(WidgetMenuItem item, bool toBottom)
    {
        int index = _items.IndexOf(item);
        if (index < 0) return;

        int target = toBottom ? _items.Count - 1 : 0;
        if (target == index) return;

        _items.RemoveAt(index);
        _items.Insert(target, item);

        SaveItems();
        ReorderChildren();
        OnItemsChanged?.Invoke();
    }

    internal void OpenContentEditor()
    {
        Framework.Service<WindowManager>().Present(
            "WidgetMenuEditor",
            new WidgetMenuEditorWindow(this)
        );
    }

    internal void OpenWidgetBrowser()
    {
        Framework.Service<WindowManager>().Present(
            "WidgetBrowser",
            new WidgetBrowserWindow(),
            window => {
                if (window.SelectedWidgetId is { } id) {
                    AddItem(id);
                }
            }
        );
    }

    internal void OpenChildEditor(WidgetMenuItem item)
    {
        ToolbarWidget? child = FindChild(item.G);

        if (child is null) return;

        Framework.Service<WidgetInstanceEditor>().OpenEditor(child);
    }

    private void LoadItems()
    {
        _items.Clear();
        _items.AddRange(DeserializeItems(GetConfigValue<string>("Items")));
    }

    private void SyncItems()
    {
        if (_isSyncingItems) return;

        List<WidgetMenuItem> parsed = DeserializeItems(GetConfigValue<string>("Items"));

        if (HasSameEntries(parsed)) {
            // Only the configuration of a child widget changed. The live
            // instance already holds the new values, so nothing to rebuild.
            for (var i = 0; i < _items.Count; i++) {
                _items[i].C = parsed[i].C;
            }

            return;
        }

        _items.Clear();
        _items.AddRange(parsed);

        RebuildChildren();
        OnItemsChanged?.Invoke();
    }

    private void SaveItems()
    {
        _isSyncingItems = true;

        try {
            SetConfigValue("Items", Compression.Compress(JsonConvert.SerializeObject(_items)));
        } finally {
            _isSyncingItems = false;
        }
    }

    private bool HasSameEntries(List<WidgetMenuItem> other)
    {
        if (other.Count != _items.Count) return false;

        for (var i = 0; i < _items.Count; i++) {
            if (_items[i].G != other[i].G || _items[i].T != other[i].T) return false;
        }

        return true;
    }

    private static List<WidgetMenuItem> DeserializeItems(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return [];

        try {
            string json = Compression.Decompress(data);

            return JsonConvert.DeserializeObject<List<WidgetMenuItem>>(json) ?? [];
        } catch (Exception e) {
            Logger.Warning($"Could not read the content of a \"Dynamic Menu (New)\" widget: {e.Message}");

            return [];
        }
    }

    private void RebuildChildren()
    {
        DisposeChildren();

        foreach (WidgetMenuItem item in _items) {
            ToolbarWidget? child = CreateChildWidget(item);

            if (child is null) continue;

            child.OnConfigValueChanged += _ => SaveChildConfig(child);

            _children.Add(child);
        }
    }

    private void ReorderChildren()
    {
        _children.Sort((a, b) => GetItemIndex(a.Id).CompareTo(GetItemIndex(b.Id)));
    }

    private int GetItemIndex(string itemId)
    {
        for (var i = 0; i < _items.Count; i++) {
            if (_items[i].G == itemId) return i;
        }

        return int.MaxValue;
    }

    private void SaveChildConfig(ToolbarWidget child)
    {
        WidgetMenuItem? item = _items.FirstOrDefault(i => i.G == child.Id);

        if (item is null) return;

        item.C = child.GetUserConfig();

        SaveItems();
        OnItemsChanged?.Invoke();
    }

    private void DisposeChildren()
    {
        foreach (ToolbarWidget child in _children) {
            child.Dispose();
        }

        _children.Clear();
    }

    private static ToolbarWidget? CreateChildWidget(WidgetMenuItem item)
    {
        var (type, info) = WidgetRegistry.RegisteredWidgets.FirstOrDefault(entry => entry.Item2.Id == item.T);

        if (type is null || info is null) {
            Logger.Warning($"Cannot find the widget type \"{item.T}\" used by a \"Dynamic Menu (New)\" widget.");

            return null;
        }

        try {
            ToolbarWidget child = (ToolbarWidget)Activator.CreateInstance(type, info, item.G, item.C)!;
            child.Setup();

            return child;
        } catch (Exception e) {
            Logger.Error($"Failed to create widget \"{item.T}\" for a \"Dynamic Menu (New)\" widget: {e.Message}");

            return null;
        }
    }
}
