namespace Una.Drawing.ExpressionParser.Tokenizer;

public enum TokenType : byte
{
    Identifier,
    String,
    Integer,
    UInt,
    Float,
    Boolean,
    OpenBracket,
    CloseBracket,
    OpenBrace,
    CloseBrace,
    Colon,
    Comma,
}