using Chat2Ex.Resources;
using Chat2Ex.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Chat2Ex.Ui;

/// <summary>
///     ImGui based auto-translate completion. The native TextInputNode has no input-text callback,
///     so this popup is driven by OnInputReceived instead of ImGui's CallbackCompletion hook.
///     SortAutoTranslate controls the ordering of the candidates.
/// </summary>
public sealed class AutoCompleteWindow : Window
{
    private List<AutoTranslateEntry> entries = [];
    private int selection;
    private Action<string>? apply;

    public AutoCompleteWindow() : base("Chat2Ex Auto Translate###chat2ex-autocomplete")
    {
        Flags = ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoFocusOnAppearing;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        IsOpen = false;
    }

    public void Show(string prefix, Action<string> onSelect)
    {
        apply = onSelect;
        entries = AutoTranslate.Matching(prefix, Plugin.Config.SortAutoTranslate).Take(50).ToList();
        selection = 0;

        if (entries.Count == 0)
        {
            IsOpen = false;
            return;
        }

        IsOpen = true;
    }

    public void Hide()
    {
        IsOpen = false;
        apply = null;
        entries.Clear();
    }

    public override void Draw()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Hide();
            return;
        }

        if (entries.Count == 0)
        {
            Hide();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            selection = (selection - 1 + entries.Count) % entries.Count;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            selection = (selection + 1) % entries.Count;

        ImGui.TextUnformatted(Language.AutoTranslate_Search_Hint);
        ImGui.Separator();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var label = entry.Title.Length > 0
                ? $"{entry.Text} ({entry.Title})##{entry.Group}/{entry.Row}"
                : $"{entry.Text}##{entry.Group}/{entry.Row}";

            if (!ImGui.Selectable(label, selection == i))
                continue;

            selection = i;
            Commit();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.Tab))
            Commit();
    }

    private void Commit()
    {
        if (apply is null || selection < 0 || selection >= entries.Count)
            return;

        var entry = entries[selection];
        var callback = apply;
        Hide();
        callback($"<at:{entry.Group},{entry.Row}>");
    }
}
