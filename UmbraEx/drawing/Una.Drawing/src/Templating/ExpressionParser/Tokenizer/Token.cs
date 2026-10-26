namespace Una.Drawing.ExpressionParser.Tokenizer;

internal struct Token
{
    public TokenType Type;
    public string    Value;
    public int       Position;
    
    public override string ToString()
    {
        return $"#{Position} {Type}({Value})";
    }
}
