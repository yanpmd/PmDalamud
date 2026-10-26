using System.Text;

namespace Una.Drawing.Templating.StyleParser;

public class StyleParser
{
    /// <summary>
    /// Constructs a <see cref="Stylesheet"/> from the given code.
    /// </summary>
    public static Stylesheet StylesheetFromCode(string code)
    {
        return (new StyleParser(Tokenizer.Tokenize(code))).ParseStylesheet();
    }

    /// <summary>
    /// Constructs a <see cref="Style"/> from the given code.
    /// </summary>
    public static Style StyleFromCode(string code)
    {
        return (new StyleParser(Tokenizer.Tokenize(code))).ParseStyle();
    }

    private readonly TokenStream _stream;
    private readonly Stylesheet  _stylesheet;

    private readonly List<(string selector, Style style)> _deferredRules = [];

    private StyleParser(TokenStream stream)
    {
        _stream     = stream;
        _stylesheet = new([]);
    }

    private Stylesheet ParseStylesheet()
    {
        if (_stream.IsEmpty) return _stylesheet;

        while (!_stream.IsEof) {
            Token? token = _stream.Peek();
            if (token == null) break;

            switch (token.Value.Type) {
                case TokenType.Keyword:
                    ParseKeyword();
                    continue;
                case TokenType.Selector:
                    ParseRule();
                    continue;
            }

            throw new Exception($"Unexpected token {token.Value}");
        }

        return _stylesheet;
    }

    private void ParseKeyword()
    {
        Token  keyword     = _stream.Consume(TokenType.Keyword);
        string keywordName = keyword.Value.ToLowerInvariant();

        switch (keywordName) {
            case "import":
                ParseImportStatement();
                return;
        }

        throw new Exception($"Undefined keyword {keywordName}.");
    }

    private void ParseImportStatement()
    {
        string name = _stream.Consume(TokenType.String).Value.ToLowerInvariant().Trim();
        _stream.Consume(TokenType.Semicolon);

        if (!StylesheetRegistry.Exists(name)) {
            throw new Exception($"Failed to import stylesheet '{name}': No such stylesheet exists.");
        }
        
        _stylesheet.ImportFrom(StylesheetRegistry.Get(name));
    }

    private void ParseRule(string? parentSelector = null, bool defer = false)
    {
        string selector = _stream.Consume(TokenType.Selector).Value.Trim();

        if (selector.StartsWith('&')) {
            selector = selector.Substring(1, selector.Length - 1);
            if (parentSelector == null) {
                throw new Exception("Unexpected ampersand in selector.");
            }
            
            selector = $"{parentSelector}{selector}";
        } else {
            selector = $"{parentSelector ?? ""} {selector}";
        }

        _stream.Consume(TokenType.OpenBrace);
        Style style = ParseStyle(selector);

        if (_stream.IsEof) {
            throw new Exception("Unexpected end of file while parsing rule. Expected '}'.");
        }

        _stream.Consume(TokenType.CloseBrace);
        
        if (defer) {
            _deferredRules.Add((selector, style));
            return;
        }
        
        _stylesheet.AddRule(selector, style);
        if (_deferredRules.Count <= 0) return;

        for (var i = 0; i < _deferredRules.Count; i++) {
            (string qs, Style s) = _deferredRules[i];
            _stylesheet.AddRule(qs, s);
        }
        
        _deferredRules.Clear();
    }

    private Style ParseStyle(string? parentSelector = null)
    {
        Style style = new();

        while (true) {
            if (_stream.IsEof || _stream.Peek()?.Type == TokenType.CloseBrace) {
                return style;
            }

            if (_stream.Peek()?.Type == TokenType.Identifier) {
                string identifier = _stream.Consume(TokenType.Identifier).Value;
                _stream.Consume(TokenType.Colon);
                List<Token> tokens = ParseStyleValue();
                _stream.Consume(TokenType.Semicolon);

                StyleAttributeParser.ApplyRule(style, identifier, tokens);
                continue;
            }

            if (_stream.Peek()?.Type == TokenType.Selector) {
                if (parentSelector == null) throw new Exception("Unexpected selector in inline style.");
                ParseRule(parentSelector, true);
                continue;
            }

            throw new Exception($"Unexpected token \"{_stream.Peek()?.Type}\" in style.");
        }
    }

    private List<Token> ParseStyleValue()
    {
        List<Token> tokens = [];

        while (true) {
            if (_stream.IsEof || _stream.Peek()?.Type is TokenType.Semicolon or TokenType.CloseBrace) {
                return tokens;
            }

            // TODO: Add function support (identifier + open parenthesis + arguments + close parenthesis)
            tokens.Add(_stream.Consume(_stream.Peek()!.Value.Type));
        }
    }
}