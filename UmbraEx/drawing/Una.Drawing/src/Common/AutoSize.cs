namespace Una.Drawing;

public enum AutoSize : byte
{
    /// <summary>
    /// Automatically size the element to fit its content on the configured axis.
    /// If the <see cref="Style.Size"/> is set to 0 on either axis, the element
    /// will be sized to fit its content by default on that axis.
    /// </summary>
    Fit,
    
    /// <summary>
    /// Expands the element to fill the available space on the configured axis.
    /// If more than one element is set to <see cref="AutoSize.Grow"/>, the
    /// available space will be divided equally among them.
    /// </summary>
    Grow,
}
