namespace Umbra.Widgets;

/// <summary>
/// A config variable that holds the path to an image file on disk that is used
/// as a custom icon.
/// </summary>
public class ImagePathWidgetConfigVariable(
    string  id,
    string  name,
    string? description,
    string  defaultValue = ""
) : WidgetConfigVariable<string>(id, name, description, defaultValue)
{
    /// <inheritdoc/>
    protected override string Sanitize(object? value)
    {
        return value is string str ? str.Trim() : string.Empty;
    }
}
