namespace Una.Drawing;

public enum ImageScaleMode : byte
{
    /// <summary>
    /// Adapts the image scale to fit the size of the node.
    /// </summary>
    Adapt,
    
    /// <summary>
    /// Render the image at its original size.
    /// </summary>
    Original,
}
