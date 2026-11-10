using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Nodes;
using AetherBags.Compat.Search;
using KamiToolKit.Addons;

namespace AetherBags.Nodes.Color;

public class ColorInputRow : HorizontalListNode
{
    private ColorPickerAddon? _colorPickerAddon;
    private readonly LabelTextNode _labelTextNode;
    private readonly ColorPreviewButtonNode _colorPreview;

    public ColorInputRow()
    {
        InitializeColorPicker();

        _colorPreview = new ColorPreviewButtonNode { Size = new Vector2(28) };
        _labelTextNode = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Position = new Vector2(28, 0),
            Height = 28,
        };

        var node = _colorPreview;

        node.OnClick = () =>
        {
            var snapshot = CurrentColor;

            if (_colorPickerAddon is not null)
            {
                _colorPickerAddon.InitialColor = snapshot;
                _colorPickerAddon.DefaultColor = DefaultColor;
                _colorPickerAddon.Toggle();

                _colorPickerAddon.OnColorConfirmed = color =>
                {
                    CurrentColor = color;
                    node.Color = color;
                    OnColorConfirmed?.Invoke(color);
                };

                _colorPickerAddon.OnColorPreviewed = color =>
                {
                    node.Color = color;
                    OnColorPreviewed?.Invoke(color);
                };

                _colorPickerAddon.OnColorCancelled = () =>
                {
                    CurrentColor = snapshot;
                    node.Color = snapshot;
                    OnColorCanceled?.Invoke(snapshot);
                };
            }
        };

        _colorPreview.AttachNode(this);
        _labelTextNode.AttachNode(this);
    }

    private void InitializeColorPicker() {
        if (_colorPickerAddon is not null) return;

        _colorPickerAddon = new ColorPickerAddon {
            InternalName = "ColorPicker_AetherBags",
            Title = Localization.Get("ColorPicker.Title"),
        };
    }

    private bool _isEnabled = true;

    /// <summary>
    /// Gets or sets whether this row accepts input.
    /// </summary>
    /// <remarks>
    /// Layout nodes no longer expose an enabled state upstream, so this is implemented by forwarding the state to
    /// the child nodes and dimming the row.
    /// </remarks>
    public bool IsEnabled {
        get => _isEnabled;
        set {
            _isEnabled = value;

            _colorPreview.IsEnabled = value;

            // The row itself carries no texture, so dimming it would have no effect. Dim the children instead.
            _labelTextNode.Alpha = value ? 1.0f : 0.5f;
            _colorPreview.Alpha = value ? 1.0f : 0.5f;
        }
    }

    protected override void Dispose(bool isNativeDestructor) {
        base.Dispose(isNativeDestructor);

        _colorPickerAddon?.Dispose();
        _colorPickerAddon = null;
    }

    public required string Label
    {
        get;
        set
        {
            field = value;
            _labelTextNode.String = value;
        }
    }

    public required Vector4 CurrentColor
    {
        get;
        set
        {
            field = value;
            _colorPreview.Color = value;
        }
    }

    public required Vector4 DefaultColor { get; set; }
    public Action<Vector4>? OnColorConfirmed { get; set; }
    public Action<Vector4>? OnColorCanceled { get; set; }
    public Action<Vector4>? OnColorChange { get; set; }
    public Action<Vector4>? OnColorPreviewed { get; set; }
}