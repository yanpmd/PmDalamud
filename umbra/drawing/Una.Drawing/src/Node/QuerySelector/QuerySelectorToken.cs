namespace Una.Drawing;

internal class QuerySelectorToken(QuerySelectorTokenType type, string value)
{
    public QuerySelectorTokenType Type  { get; } = type;
    public string                 Value { get; } = value;
}
