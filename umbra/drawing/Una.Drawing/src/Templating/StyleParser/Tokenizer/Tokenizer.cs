namespace Una.Drawing.Templating.StyleParser;

internal sealed class Tokenizer
{
    public static TokenStream Tokenize(string code)
    {
        return (new Tokenizer(code.Trim())).Tokenize();
    }

    private readonly string      _code;
    private readonly List<Token> _tokens = [];

    private int _position;
    private int _lineNumber   = 1;
    private int _columnNumber = 1;

    private Tokenizer(string code)
    {
        this._code = code;
    }

    private TokenStream Tokenize()
    {
        while (!IsEof) {
            if (ProcessWhitespace()) continue;
            if (ProcessString()) continue;
            if (ProcessSelector()) continue;
            if (ProcessNumber()) continue;
            if (ProcessComment()) continue;
            if (ProcessSymbol()) continue;
            if (ProcessVariable()) continue;
            if (ProcessBoolean()) continue;
            if (ProcessKeyword()) continue;
            if (ProcessIdentifier()) continue;

            throw new Exception(
                $"Unexpected character '{_code[_position]}' at line {_lineNumber}, column {_columnNumber}");
        }

        return new TokenStream(_tokens);
    }

    private bool IsEof { get => _position == _code.Length; }

    private bool ProcessBoolean()
    {
        bool isTrue  = Peek(4).ToLowerInvariant().Equals("true");
        bool isFalse = Peek(5).ToLowerInvariant().Equals("false");

        if (!isTrue && !isFalse) {
            return false;
        }

        int length = isTrue ? 4 : 5;

        // If the next character is a letter, it's not a boolean.
        if (_position + length < _code.Length && char.IsLetter(_code[_position + length])) {
            return false;
        }

        int columnStart = _columnNumber;

        _position     += length;
        _columnNumber += length;

        _tokens.Add(new() {
            Type        = TokenType.Boolean,
            Value       = isTrue ? "true" : "false",
            LineStart   = _lineNumber,
            ColumnStart = columnStart,
            LineEnd     = _lineNumber,
            ColumnEnd   = _columnNumber,
        });

        return true;
    }

    private bool ProcessKeyword()
    {
        char c = _code[_position];

        if (c != '@') {
            return false;
        }

        _position++;
        _columnNumber++;

        int positionStart = _position;
        int columnStart   = _columnNumber;

        while (!IsEof) {
            c = _code[_position];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') {
                _position++;
                _columnNumber++;
            } else {
                break;
            }
        }

        string value = _code.Substring(positionStart, _position - positionStart).ToLowerInvariant();

        _tokens.Add(new() {
            Type        = TokenType.Keyword,
            Value       = value,
            LineStart   = _lineNumber,
            ColumnStart = columnStart,
            LineEnd     = _lineNumber,
            ColumnEnd   = _columnNumber,
        });

        return true;
    }

    private bool ProcessSelector()
    {
        char c = _code[_position];
        char p = _position > 1 ? _code[_position - 1] : ' ';

        if (char.IsLetter(p) || (c != '#' && c != '.' && c != ':' && c != '&' && c != '>')) {
            return false;
        }

        int positionStart = _position;
        int columnStart   = _columnNumber;

        _position++;
        _columnNumber++;

        while (!IsEof) {
            c = _code[_position];
            if (char.IsLetterOrDigit(c) ||
                c == '_' || c == '-' || c == ':' || c == '.' || c == '#' || c == '>' || c == ' '
            ) {
                _position++;
                _columnNumber++;
            } else {
                break;
            }
        }

        string value = _code.Substring(positionStart, _position - positionStart).Trim();

        _tokens.Add(new() {
            Type        = TokenType.Selector,
            Value       = value,
            LineStart   = _lineNumber,
            ColumnStart = columnStart,
            LineEnd     = _lineNumber,
            ColumnEnd   = _columnNumber,
        });

        return true;
    }

    private bool ProcessIdentifier()
    {
        char c = _code[_position];

        if (!char.IsLetter(c) && c != '_') {
            return false;
        }

        int positionStart = _position;
        int columnStart   = _columnNumber;

        while (!IsEof) {
            c = _code[_position];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') {
                _position++;
                _columnNumber++;
            } else {
                break;
            }
        }

        string value = _code.Substring(positionStart, _position - positionStart);

        _tokens.Add(new() {
            Type        = TokenType.Identifier,
            Value       = value,
            LineStart   = _lineNumber,
            ColumnStart = columnStart,
            LineEnd     = _lineNumber,
            ColumnEnd   = _columnNumber,
        });

        return true;
    }

    private bool ProcessSymbol()
    {
        char c = _code[_position];

        TokenType? t = c switch {
            '<' => TokenType.LessThan,
            '>' => TokenType.GreaterThan,
            '=' => TokenType.Assignment,
            '/' => TokenType.Slash,
            '(' => TokenType.OpenParenthesis,
            ')' => TokenType.CloseParenthesis,
            ',' => TokenType.Comma,
            ':' => TokenType.Colon,
            ';' => TokenType.Semicolon,
            '{' => TokenType.OpenBrace,
            '}' => TokenType.CloseBrace,
            '[' => TokenType.OpenBracket,
            ']' => TokenType.CloseBracket,
            '&' => TokenType.Ampersand,
            _   => null
        };

        if (t is null) {
            return false;
        }

        _position++;
        _columnNumber++;
        _tokens.Add(new() {
            Type        = t.Value,
            LineStart   = _lineNumber,
            ColumnStart = _columnNumber - 1,
            LineEnd     = _lineNumber,
            ColumnEnd   = _columnNumber,
        });

        return true;
    }

    private bool ProcessVariable()
    {
        char c  = !IsEof ? _code[_position] : ' ';
        char c2 = _position + 1 < _code.Length ? _code[_position + 1] : ' ';

        if (!(c == '$' && char.IsLetter(c2))) {
            return false;
        }

        int positionStart = _position;
        int columnStart   = _columnNumber;

        _position++;
        _columnNumber++;

        while (!IsEof) {
            c = _code[_position];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') {
                _position++;
                _columnNumber++;
            } else {
                break;
            }
        }

        string value = _code.Substring(positionStart, _position - positionStart);

        _tokens.Add(new() {
            Type        = TokenType.Variable,
            Value       = value,
            LineStart   = _lineNumber,
            ColumnStart = columnStart,
            LineEnd     = _lineNumber,
            ColumnEnd   = _columnNumber,
        });

        return true;
    }

    private bool ProcessComment()
    {
        char c  = !IsEof ? _code[_position] : ' ';
        char c2 = _position + 1 < _code.Length ? _code[_position + 1] : ' ';

        if (c != '/' || IsEof) {
            return false;
        }

        if (c2 != '/' && c2 != '*') {
            return false;
        }

        _position++;
        _columnNumber++;

        if (_code[_position] == '/') {
            while (!IsEof && c != '\n') {
                c = _code[_position];
                _position++;
                _columnNumber++;
            }

            return true;
        }

        if (_code[_position] != '*') {
            return false;
        }

        _position++;
        _columnNumber++;

        while (!IsEof) {
            c = _code[_position];
            if (c == '*' && !IsEof && _code[_position + 1] == '/') {
                _position     += 2;
                _columnNumber += 2;
                return true;
            }

            if (c == '\n') {
                _lineNumber++;
                _columnNumber = 1;
            } else {
                _columnNumber++;
            }

            _position++;
        }

        return false;
    }

    private bool ProcessNumber()
    {
        char c = _code[_position];

        if (!char.IsDigit(c) && c != '.' && c != '-') return false;

        int positionStart = _position;
        int columnStart   = _columnNumber;

        // Check for hexadecimal (starts with 0x or 0X)
        if (c == '0' && _position + 1 < _code.Length && (_code[_position + 1] == 'x' || _code[_position + 1] == 'X')) {
            _position     += 2;
            _columnNumber += 2;

            int hexStart = _position;
            while (!IsEof && IsHexDigit(_code[_position])) {
                _position++;
                _columnNumber++;
            }

            if (hexStart == _position)
                throw new Exception($"Invalid hex number at line {_lineNumber}, column {_columnNumber}");

            string hexValue = _code.Substring(hexStart, _position - hexStart).Replace("_", "");

            _tokens.Add(new() {
                Type        = TokenType.UInt,
                LineStart   = _lineNumber,
                ColumnStart = columnStart,
                LineEnd     = _lineNumber,
                ColumnEnd   = _columnNumber,
                Value       = hexValue,
            });

            return true;
        }

        bool isFloat    = false;
        bool isExponent = false;

        if (c is '-' or '+') {
            _position++;
            _columnNumber++;
        }

        while (true) {
            c = IsEof ? ' ' : _code[_position];
            if (IsEof || (!char.IsDigit(c) && c != '.' && c != 'e' && c != 'E' && c != '_')) {
                string value = _code.Substring(positionStart, _position - positionStart).Replace("_", "");

                TokenType type = (isFloat || isExponent) ? TokenType.Double : TokenType.Integer;

                _tokens.Add(new() {
                    Type        = type,
                    LineStart   = _lineNumber,
                    ColumnStart = columnStart,
                    LineEnd     = _lineNumber,
                    ColumnEnd   = _columnNumber,
                    Value       = value.Replace("_", "").Trim(),
                });

                return true;
            }

            switch (c) {
                case '.' when isFloat:
                    throw new Exception($"Invalid number at line {_lineNumber}, column {_columnNumber}");
                case '.':
                    isFloat = true;
                    break;
                case 'e' or 'E' when isExponent:
                    throw new Exception($"Invalid number at line {_lineNumber}, column {_columnNumber}");
                case 'e' or 'E':
                    isExponent = true;
                    break;
            }

            _position++;
            _columnNumber++;
        }
    }

    private static bool IsHexDigit(char c)
    {
        return char.IsDigit(c) || c is >= 'a' and <= 'f' || c is >= 'A' and <= 'F';
    }

    private bool ProcessString()
    {
        char delimiter = _code[_position];
        if (delimiter != '\'' && delimiter != '"') {
            return false;
        }

        _position++;
        _columnNumber++;

        int positionStart = _position;
        int lineStart     = _lineNumber;
        int columnStart   = _columnNumber;

        while (!IsEof) {
            char c = _code[_position];
            if (c == delimiter) {
                _tokens.Add(new() {
                    Type        = TokenType.String,
                    Value       = _code.Substring(positionStart, _position - positionStart),
                    LineStart   = lineStart,
                    ColumnStart = columnStart,
                    LineEnd     = _lineNumber,
                    ColumnEnd   = _columnNumber,
                });

                _position++;
                _columnNumber++;

                return true;
            }

            if (c == '\\') {
                _position++;
                _columnNumber++;

                if (IsEof) {
                    return false;
                }

                c = _code[_position];
            }

            if (c == '\n') {
                _lineNumber++;
                _columnNumber = 1;
            } else {
                _columnNumber++;
            }

            _position++;
        }

        throw new Exception($"Unterminated string at line {_lineNumber}, column {_columnNumber}");
    }

    /// <summary>
    /// Process whitespace characters until a non-whitespace character is found.
    /// </summary>
    private bool ProcessWhitespace()
    {
        char c = _code[_position];

        while (!IsEof) {
            if (false == char.IsWhiteSpace(c)) {
                return false;
            }

            if (c == '\n') {
                _lineNumber++;
                _columnNumber = 1;
            } else {
                _columnNumber++;
            }

            _position++;
            c = _code[_position];
        }

        return true;
    }

    private string Peek(ushort length = 1)
    {
        if (IsEof) {
            return string.Empty;
        }

        if (_position + length > _code.Length) {
            length = (ushort)(_code.Length - _position);
        }

        return _code.Substring(_position, length);
    }
}