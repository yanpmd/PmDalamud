namespace Una.Drawing;

internal enum QuerySelectorTokenType
{
    /// <summary>
    /// An identifier of a node.
    /// </summary>
    Identifier,

    /// <summary>
    /// A class-name of a node.
    /// </summary>
    Class,

    /// <summary>
    /// An asterisk, implying to match all nodes.
    /// </summary>
    All,
    
    /// <summary>
    /// The next tokens represent direct children of the current node.
    /// </summary>
    Child,

    /// <summary>
    /// The next tokens represent nested children (recursively) of the current node.
    /// </summary>
    DeepChild,

    /// <summary>
    /// Represents a pseudo-class of the current node.
    /// </summary>
    TagList,

    /// <summary>
    /// Represents a separator in the current node.
    /// </summary>
    Separator,
}
