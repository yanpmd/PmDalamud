using Dalamud.Interface.ImGuiFileDialog;
using System.IO;

namespace Umbra.Windows.Components;

/// <summary>
/// Shows a file dialog that lets the user pick an image file.
/// ImGui file dialogs have to be drawn outside of any window, which is why this
/// is a service with its own draw handler.
/// </summary>
[Service]
internal sealed class ImagePickerService
{
    private FileDialogManager? _dialog;
    private Action<string?>?   _callback;

    /// <summary>
    /// Opens a file dialog that lets the user pick a single image file.
    /// The callback is invoked with the selected path, or null when the dialog
    /// was cancelled.
    /// </summary>
    public void PickImage(string? startPath, Action<string?> callback)
    {
        _dialog   = new();
        _callback = callback;

        _dialog.OpenFileDialog(
            I18N.Translate("Widgets.Standard.Config.CustomImage.DialogTitle"),
            ".png,.jpg,.jpeg,.bmp,.gif",
            OnFilePicked,
            1,
            GetStartDirectory(startPath)
        );
    }

    [OnDraw(Int32.MaxValue - 100)]
    private void OnDraw()
    {
        if (null == _dialog) return;

        // Keep the dialog consistent with the rest of the UI.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1);
        ImGui.PushStyleColor(ImGuiCol.Border, Color.GetNamedColor("Window.Border"));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Color.GetNamedColor("Window.Background"));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Color.GetNamedColor("Window.BackgroundLight"));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Color.GetNamedColor("Input.Background"));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Color.GetNamedColor("Input.BackgroundHover"));
        ImGui.PushStyleColor(ImGuiCol.Text, Color.GetNamedColor("Input.Text"));

        try {
            _dialog.Draw();
        } catch (Exception e) {
            Logger.Error($"Failed to draw the image picker dialog: {e.Message}");
            _dialog   = null;
            _callback = null;
        }

        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(4);
    }

    private void OnFilePicked(bool isOk, List<string> files)
    {
        Action<string?>? callback = _callback;

        _dialog   = null;
        _callback = null;

        callback?.Invoke(isOk ? files.FirstOrDefault() : null);
    }

    private static string? GetStartDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try {
            string? dir = Path.GetDirectoryName(path);

            return null != dir && Directory.Exists(dir) ? dir : null;
        } catch {
            return null;
        }
    }
}
