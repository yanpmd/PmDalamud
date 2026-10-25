using Una.Drawing.ExpressionParser.Tokenizer;

namespace Una.Drawing.ExpressionParser;

internal class ExpressionParser
{
    internal static dynamic? Parse(string expression, Dictionary<string, string>? arguments = null)
    {
        if (arguments != null) {
            foreach ((string argName, string argValue) in arguments) {
                expression = expression.Replace($"${{{argName}}}", argValue,
                    StringComparison.InvariantCultureIgnoreCase);
            }
        }

        return new ExpressionParser(Tokenizer.Tokenizer.Tokenize(expression)).Parse();
    }
    
    private readonly TokenStream _stream;
    
    private ExpressionParser(TokenStream stream)
    {
        _stream = stream;
    }

    private dynamic? Parse()
    {
        if (_stream.IsEmpty) return null;
        if (_stream.Size == 1) return ParseScalar();
        
        return ParseObject();
    }

    private dynamic? ParseObject()
    {
        if (_stream.Peek()?.Type == TokenType.OpenBracket) {
            _stream.Advance();
            List<object> values = [];
            
            while (!_stream.IsEof) {
                if (_stream.Peek()?.Type == TokenType.CloseBracket) {
                    _stream.Advance();
                    return values;
                }
                
                if (_stream.Peek()?.Type == TokenType.Comma) {
                    _stream.Advance();
                    continue;
                }

                if (_stream.Peek()?.Type is TokenType.OpenBracket or TokenType.OpenBrace) {
                    values.Add(ParseObject());
                    continue;
                }

                values.Add(ParseScalar());
            }
            
            throw new Exception("Unexpected end of array expression");
        }

        if (_stream.Peek()?.Type == TokenType.OpenBrace) {
            _stream.Advance();
            Dictionary<string, object> values = [];

            while (!_stream.IsEof) {
                if (_stream.Peek()?.Type == TokenType.CloseBrace) {
                    _stream.Advance();
                    return values;
                }
                
                if (_stream.Peek()?.Type == TokenType.Comma) {
                    _stream.Advance();
                    continue;
                }

                string key;
                if (_stream.Peek()?.Type == TokenType.Identifier) {
                    key = _stream.Consume(TokenType.Identifier).Value;
                } else if (_stream.Peek()?.Type == TokenType.String) {
                    key = _stream.Consume(TokenType.String).Value;
                } else {
                    throw new Exception($"Unexpected token {_stream.Peek()} in dictionary key.");
                }

                _stream.Consume(TokenType.Colon);

                values.Add(key,
                    _stream.Peek()?.Type is TokenType.OpenBracket or TokenType.OpenBrace
                        ? ParseObject()
                        : ParseScalar());
            }
            
            throw new Exception("Unexpected end of object expression");
        }

        throw new Exception($"Unexpected token {_stream.Peek()}");
    }
    
    private dynamic? ParseScalar()
    {
        Token token = _stream.Consume(_stream.Peek()!.Value.Type);

        switch (token.Type) {
            case TokenType.String:
            case TokenType.Identifier:
                return token.Value;
            case TokenType.Integer:
                return Convert.ToInt32(token.Value);
            case TokenType.UInt:
                return Convert.ToUInt32(token.Value);
            case TokenType.Float:
                return Convert.ToSingle(token.Value);
            case TokenType.Boolean:
                return Convert.ToBoolean(token.Value);
        }
        
        throw new Exception($"Unexpected token {token}.");
    }
}
