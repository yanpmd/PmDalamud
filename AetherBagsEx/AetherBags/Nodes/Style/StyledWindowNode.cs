using System.Numerics;
using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Style;

/// <summary>
/// A <see cref="WindowNode"/> that takes its chrome parameters from an
/// <see cref="InventoryStyle"/> instead of the hardcoded WindowA values.
/// Only parameters that are safe to change are touched - the texture family
/// itself always stays on <c>ui/uld/WindowA_*</c>.
/// </summary>
public sealed class StyledWindowNode : WindowNode
{
    public StyledWindowNode() : this(StylePresets.Current) { }

    public StyledWindowNode(InventoryStyle style) => ApplyStyle(style);

    /// <summary>
    /// Hides the window title and subtitle in the top left corner. Used by the bag
    /// windows, where the title only ever repeated the plugin name.
    /// </summary>
    public bool HideTitle
    {
        get => _hideTitle;
        set
        {
            _hideTitle = value;
            ApplyTitleVisibility();
        }
    }

    private bool _hideTitle;

    public void ApplyStyle(InventoryStyle style)
    {
        if (style is null) return;

        BackgroundNode.Offsets = style.WindowOffsets;
        BorderNode.Offsets = style.WindowOffsets;
        BackgroundNode.AddColor = style.WindowTint;
        BackgroundNode.Alpha = style.WindowAlpha;

        BackgroundImageNode.IsVisible = style.ShowWindowGradation;
        BackgroundImageNode.AddColor = style.WindowTint;
        BackgroundImageNode.Alpha = style.WindowAlpha;

        DividingLineNode.IsVisible = style.ShowDividingLine;

        TitleNode.FontType = style.TitleFontType;
        TitleNode.FontSize = style.TitleFontSize;
        TitleNode.TextColor = ColorHelper.GetColor(style.TitleColorId);
        TitleNode.TextOutlineColor = ColorHelper.GetColor(style.TitleOutlineColorId);

        ApplyTitleVisibility();
    }

    public override void SetTitle(string title, string? subtitle = null)
    {
        base.SetTitle(title, subtitle);

        // SetTitle re-shows the title nodes, so re-apply the hidden state after it.
        ApplyTitleVisibility();
    }

    private void ApplyTitleVisibility()
    {
        TitleNode.IsVisible = !_hideTitle;
        SubtitleNode.IsVisible = !_hideTitle;
    }
}
