namespace Una.Drawing.ExpressionParser.Tokenizer;

internal sealed class Tokenizer
{
    internal static TokenStream Tokenize(string expression)
    {
        return new Tokenizer(expression).Tokenize();
    }

    private readonly List<Token> _tokens = [];
    private readonly string      _expression;

    private int _position;

    private Tokenizer(string expression)
    {
        _expression = expression;
        _position   = 0;
    }

    private TokenStream Tokenize()
    {
        while (!IsEof) {
            if (ProcessWhitespace()) continue;
            if (ProcessString()) continue;
            if (ProcessNumber()) continue;
            if (ProcessSymbol()) continue;
            if (ProcessBoolean()) continue;
            if (ProcessIdentifier()) continue;

            throw new Exception($"Unexpected character \"{Current}\" at position {(_position + 1)}.");
        }

        return new TokenStream(_tokens);
    }

    private bool IsEof    => _position >= _expression.Length;
    private char Current  => _expression[_position];
    private char Next     => _position < _expression.Length - 1 ? _expression[_position + 1] : '\0';
    private char Previous => _position > 0 ? _expression[_position - 1] : '\0';

    private bool ProcessWhitespace()
    {
        if (!char.IsWhiteSpace(Current)) return false;

        _position++;
        return true;
    }

    private bool ProcessSymbol()
    {
        char c = Current;

        TokenType? type = c switch {
            '{' => TokenType.OpenBrace,
            '}' => TokenType.CloseBrace,
            '[' => TokenType.OpenBracket,
            ']' => TokenType.CloseBracket,
            ':' => TokenType.Colon,
            ',' => TokenType.Comma,
            _   => null,
        };

        if (type == null) return false;

        _tokens.Add(new() { Type = type.Value, Value = c.ToString(), Position = _position });
        _position++;

        return true;
    }

    private bool ProcessBoolean()
    {
        bool isTrue  = Peek(4).ToLowerInvariant().Equals("true");
        bool isFalse = Peek(5).ToLowerInvariant().Equals("false");

        if (!isTrue && !isFalse) {
            return false;
        }

        int length = isTrue ? 4 : 5;

        // If the next character is a letter, it's not a boolean.
        if (_position + length < _expression.Length && char.IsLetter(_expression[_position + length])) {
            return false;
        }

        _position += length;

        _tokens.Add(
            new() { Type = TokenType.Boolean, Value = isTrue ? "true" : "false", Position = _position - length });

        return true;
    }

    private bool ProcessString()
    {
        char delimiter = _expression[_position];
        if (delimiter != '\'') {
            return false;
        }

        _position++;

        int positionStart = _position;

        while (!IsEof) {
            char c = _expression[_position];
            if (c == delimiter) {
                _tokens.Add(new() {
                    Type     = TokenType.String,
                    Value    = _expression.Substring(positionStart, _position - positionStart),
                    Position = positionStart,
                });

                _position++;

                return true;
            }

            if (c == '\\') {
                _position++;
                _position++;

                if (IsEof) {
                    return false;
                }
            }

            _position++;
        }

        throw new Exception($"Unterminated string at {_position}");
    }

    private bool ProcessNumber()
    {
        char c = _expression[_position];

        if (!char.IsDigit(c) && c != '.' && c != '-') return false;

        int positionStart = _position;

        // Check for hexadecimal (starts with 0x or 0X)
        if (c == '0' && _position + 1 < _expression.Length &&
            (_expression[_position + 1] == 'x' || _expression[_position + 1] == 'X')) {
            _position += 2;

            int hexStart = _position;
            while (!IsEof && IsHexDigit(_expression[_position])) {
                _position++;
            }

            if (hexStart == _position)
                throw new Exception($"Invalid hex number at position {positionStart}");

            string hexValue = _expression.Substring(hexStart, _position - hexStart).Replace("_", "");

            _tokens.Add(new() { Type = TokenType.UInt, Value = hexValue, Position = positionStart });

            return true;
        }

        bool isFloat    = false;
        bool isExponent = false;

        if (c is '-' or '+') {
            _position++;
        }

        while (true) {
            c = IsEof ? ' ' : _expression[_position];
            if (IsEof || (!char.IsDigit(c) && c != '.' && c != 'e' && c != 'E' && c != '_')) {
                string value = _expression.Substring(positionStart, _position - positionStart).Replace("_", "");

                TokenType type = (isFloat || isExponent) ? TokenType.Float : TokenType.Integer;

                _tokens.Add(new() { Type = type, Value = value.Replace("_", "").Trim(), Position = positionStart });

                return true;
            }

            switch (c) {
                case '.' when isFloat:
                    throw new Exception($"Invalid number at position {_position}");
                case '.':
                    isFloat = true;
                    break;
                case 'e' or 'E' when isExponent:
                    throw new Exception($"Invalid number at position {_position}");
                case 'e' or 'E':
                    isExponent = true;
                    break;
            }

            _position++;
        }
    }

    private static bool IsHexDigit(char c)
    {
        return char.IsDigit(c) || c is >= 'a' and <= 'f' || c is >= 'A' and <= 'F';
    }

    private bool ProcessIdentifier()
    {
        if (!char.IsLetter(Current) && Current != '_') {
            return false;
        }

        int positionStart = _position;

        while (!IsEof) {
            if (char.IsLetterOrDigit(Current) || Current == '_' || Current == '-') {
                _position++;
            } else {
                break;
            }
        }

        string value = _expression.Substring(positionStart, _position - positionStart);

        _tokens.Add(new() { Type = TokenType.Identifier, Value = value, Position = positionStart });

        return true;
    }

    private string Peek(ushort length = 1)
    {
        if (IsEof) {
            return string.Empty;
        }

        if (_position + length > _expression.Length) {
            length = (ushort)(_expression.Length - _position);
        }

        return _expression.Substring(_position, length);
    }
}