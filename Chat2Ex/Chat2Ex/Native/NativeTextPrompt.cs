using System.Numerics;
using Chat2Ex.Resources;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace Chat2Ex.Native;

/// <summary>
///     Small native input dialog, used for renaming tabs and other short text prompts.
/// </summary>
public sealed class NativeTextPrompt : NativeAddon
{
    private readonly Plugin Plugin;
    private readonly string initialText;
    private readonly Action<string?> onSubmit;

    private TextInputNode? inputNode;
    private TextButtonNode? confirmButton;
    private TextButtonNode? cancelButton;
    private bool focused;

    /// <param name="plugin">Owning plugin.</param>
    /// <param name="initialText">Text to prefill.</param>
    /// <param name="onSubmit">Invoked with the entered text, or <c>null</c> when cancelled.</param>
    public NativeTextPrompt(Plugin plugin, string initialText, Action<string?> onSubmit)
    {
        Plugin = plugin;
        this.initialText = initialText;
        this.onSubmit = onSubmit;
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        var start = ContentStartPosition;
        var size = ContentSize;

        inputNode = new TextInputNode
        {
            Position = new Vector2(start.X, start.Y + 2.0f),
            Size = new Vector2(size.X, 28.0f),
            MaxCharacters = 128,
            String = initialText,
            OnInputComplete = _ => Submit(),
            OnEscapeEntered = Cancel,
        };
        inputNode.AttachNode(this);

        var buttonWidth = (size.X - 8.0f) / 2.0f;
        var buttonY = start.Y + size.Y - 28.0f;

        confirmButton = new TextButtonNode
        {
            Position = new Vector2(start.X, buttonY),
            Size = new Vector2(buttonWidth, 28.0f),
            String = Language.Settings_Save,
            OnClick = Submit,
        };
        confirmButton.AttachNode(this);

        cancelButton = new TextButtonNode
        {
            Position = new Vector2(start.X + buttonWidth + 8.0f, buttonY),
            Size = new Vector2(buttonWidth, 28.0f),
            String = Language.Settings_Discard,
            OnClick = Cancel,
        };
        cancelButton.AttachNode(this);
    }

    protected override unsafe void OnDraw(AtkUnitBase* addon)
    {
        if (!focused)
        {
            focused = true;
            inputNode?.SetFocus();
        }

        base.OnDraw(addon);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        inputNode = null;
        confirmButton = null;
        cancelButton = null;

        base.OnFinalize(addon);
    }

    private void Submit()
    {
        var text = inputNode?.String.ToString().Trim() ?? string.Empty;
        CloseDeferred();
        onSubmit(string.IsNullOrEmpty(text) ? null : text);
    }

    private void Cancel()
    {
        CloseDeferred();
        onSubmit(null);
    }

    /// <summary>
    ///     Disposing from inside a button/enter callback would free the node tree while the game is
    ///     still processing the event, so the actual disposal is deferred to the next tick.
    /// </summary>
    private void CloseDeferred()
    {
        Plugin.Framework.RunOnTick(() =>
        {
            Dispose();
        });
    }
}
