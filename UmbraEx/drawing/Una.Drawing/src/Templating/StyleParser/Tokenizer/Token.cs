namespace Una.Drawing.Templating.StyleParser;

[StructLayout(LayoutKind.Sequential)]
internal struct Token
{
    public TokenType Type;
    public string    Value;
    public int       LineStart;
    public int       ColumnStart;
    public int       LineEnd;
    public int       ColumnEnd;

    public override string ToString()
    {
        return $"{Type}({Value}) at {LineStart}:{ColumnStart} - {LineEnd}:{ColumnEnd}";
    }
}
