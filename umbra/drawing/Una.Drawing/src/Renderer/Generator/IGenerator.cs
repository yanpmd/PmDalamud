namespace Una.Drawing.Generator;

internal interface IGenerator
{
    /// <summary>
    /// Defines the order in which the generator should be rendered.
    /// </summary>
    public int RenderOrder { get; }

    /// <summary>
    /// Generates a part of the texture for the given node.
    /// </summary>
    /// <param name="canvas">The drawing canvas.</param>
    /// <param name="node">The source node.</param>
    /// <param name="origin">The origin of the canvas.</param>
    public bool Generate(SKCanvas canvas, Node node, Vector2 origin);
}