namespace Una.Drawing;

public interface IUdtDirectiveParser
{
    /// <summary>
    /// <para>
    /// Returns the name of an attribute that this parser can handle.
    /// </para>
    /// <para>
    /// Attribute names should not contain hyphens because these are
    /// stripped from the attribute name when parsed. For example, if your
    /// directive wants to act on attribute "img-src", the name "imgsrc" should
    /// be set here.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Do NOT use existing Node property names for directives, as registered
    /// directive attributes are not passed to the constructed node.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Parses the given attribute and modifies the given Node if needed.
    /// </summary>
    public void Parse(Node node, string value);
}
