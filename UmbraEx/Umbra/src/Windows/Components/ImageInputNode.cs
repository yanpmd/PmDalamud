namespace Umbra.Windows.Components;

/// <summary>
/// Input control that lets the user pick an image file that is used as a
/// custom icon. The selected image is shown as a preview.
/// </summary>
public class ImageInputNode : BoxedInputNode
{
    public event Action<string>? OnValueChanged;

    public string Value {
        get => _value;
        set
        {
            value ??= string.Empty;

            if (_value == value) return;

            _value = value;
            OnValueChanged?.Invoke(value);

            if (IsDisposed) return;

            UpdatePreview();
        }
    }

    private string _value = string.Empty;

    public ImageInputNode(string id, string value, string label, string? description = null)
    {
        CreateNode();

        Id          = id;
        Label       = label;
        Description = description;
        Value       = value;
    }

    public ImageInputNode()
    {
        CreateNode();
    }

    private void CreateNode()
    {
        BoxNode.AppendChild(new() { ClassList = ["preview"], InheritTags = true });
        BoxNode.Tooltip = I18N.Translate("Widgets.Standard.Config.CustomImage.Tooltip");

        BoxNode.OnClick      += _ => PickImage();
        BoxNode.OnRightClick += _ => Value = string.Empty;

        UpdatePreview();
    }

    private void PickImage()
    {
        Framework
           .Service<ImagePickerService>()
           .PickImage(_value, path => Value = path ?? string.Empty);
    }

    private void UpdatePreview()
    {
        if (IsDisposed) return;

        Node? preview = BoxNode.QuerySelector(".preview");

        if (preview is null) return;

        byte[]? bytes = CustomImages.GetBytes(_value);

        preview.Style.ImageBytes = bytes;
        preview.Style.IsVisible  = bytes is not null;
        BoxNode.NodeValue        = bytes is null ? FontAwesomeIcon.Image.ToIconString() : default;
    }

    protected override void OnDisposed()
    {
        foreach (var handler in OnValueChanged?.GetInvocationList() ?? []) {
            OnValueChanged -= (Action<string>)handler;
        }

        OnValueChanged = null;

        base.OnDisposed();
    }
}
