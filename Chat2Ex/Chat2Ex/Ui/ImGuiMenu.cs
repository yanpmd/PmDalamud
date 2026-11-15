using Chat2Ex.Native;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Chat2Ex.Ui;

/// <summary>
///     ImGui replacement for the game's native context menu. Native addon callbacks only queue a
///     request; the popup is opened from UiBuilder.Draw on the next frame, so no ImGui call (and no
///     addon creation) ever happens inside a FireCallback / input-event chain.
/// </summary>
public sealed class ImGuiMenu
{
    public const string PopupId = "chat2ex-menu-popup";

    public sealed class Item
    {
        public string Name = string.Empty;
        public Action? OnClick;
        public bool Enabled = true;
        public List<Item> Children = [];
    }

    private readonly List<Item> items = [];
    private bool openPending;

    public List<Item> Items => items;

    public void Clear() => items.Clear();

    public Item Add(string name, Action? onClick = null, bool enabled = true)
    {
        var item = new Item { Name = name, OnClick = onClick, Enabled = enabled };
        items.Add(item);
        return item;
    }

    public void Request() => openPending = true;

    public void Close()
    {
        openPending = false;
        items.Clear();
    }

    public void Draw()
    {
        if (openPending)
        {
            openPending = false;
            ImGui.OpenPopup(PopupId);
        }

        using var popup = ImRaii.Popup(PopupId);
        if (!popup.Success)
            return;

        DrawItems(items);
    }

    private static void DrawItems(List<Item> list)
    {
        foreach (var item in list)
        {
            if (item.Children.Count > 0)
            {
                using var menu = ImRaii.Menu(item.Name);
                if (menu.Success)
                    DrawItems(item.Children);
                continue;
            }

            if (item.Enabled && ImGui.MenuItem(item.Name) && item.OnClick is not null)
                NativeCallback.Safe(item.OnClick, "running a menu action");
            else if (!item.Enabled)
                ImGui.TextDisabled(item.Name);
        }
    }
}
