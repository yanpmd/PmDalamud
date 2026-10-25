using System.Linq;
using System.Text;

namespace Una.Drawing.Templating.StyleParser;

internal sealed class TokenStream(List<Token> tokens)
{
    private uint _index;

    private Token Current => tokens[(int)_index];

    internal bool IsEmpty => tokens.Count == 0;

    internal bool IsEof => _index >= tokens.Count;

    /// <summary>
    /// Consumes the current token and returns it.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    internal Token Consume(TokenType type)
    {
        if (IsEof) {
            throw new Exception(
                $"Unexpected end of stream, expected {type}.");
        }

        if (Current.Type != type) {
            throw new Exception($"Unexpected token {Current}, expected {type}.");
        }

        var token = Current;
        _index++;

        return token;
    }

    internal Token? Peek(int offset = 0)
    {
        if (IsEof) return null;
        if (_index + offset >= tokens.Count) return null;

        return tokens[(int)(_index + offset)];
    }

    internal bool SequenceEquals(TokenType[] types)
    {
        if (IsEof) return false;
        if (_index + types.Length > tokens.Count) return false;

        return !types.Where((t, i) => tokens[(int)(_index + i)].Type != t).Any();
    }

    internal void Advance(int count = 1)
    {
        for (var i = 0; i < count; ++i) {
            if (IsEof) throw new Exception("Unexpected end of stream.");
            Consume(Current.Type);
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        
        foreach (var token in tokens) {
            sb.AppendLine(token.ToString());
        }
        
        return sb.ToString();
    }
}