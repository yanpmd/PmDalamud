namespace Una.Drawing;

public interface IUdtAttributeValueParser : IDisposable
{
    /// <summary>
    /// Returns a modified string based on the given value.
    /// </summary>
    /// <param name="value">The original value.</param>
    /// <returns>The modified value</returns>
    public string Parse(string value);
}
