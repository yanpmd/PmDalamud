namespace Una.Drawing.Templating.StyleParser;

internal enum TokenType : byte
{
    Identifier,
    Selector,
    String,
    Integer,
    UInt,
    Double,
    Boolean,
    LessThan,
    GreaterThan,
    Colon,
    Semicolon,
    Assignment,
    Slash,
    OpenParenthesis,
    CloseParenthesis,
    OpenBrace,
    CloseBrace,
    OpenBracket,
    CloseBracket,
    Comma,
    Keyword,
    Ampersand,
    Variable,
}
