namespace Prismica.Core.Formula;

public sealed class FormulaParser
{
    private readonly string _source;
    private int _pos;
    private int _length;

    public FormulaParser(string source)
    {
        _source = source;
        _length = source.Length;
    }

    public FormulaAst Parse()
    {
        _pos = 0;
        var node = ParseExpression();
        SkipWhitespace();
        if (_pos < _length) throw new FormatException($"Unexpected character at position {_pos}: '{_source[_pos]}'");
        return new FormulaAst { Root = node, Source = _source, Length = _length };
    }

    private AstNode ParseExpression() => ParseTernary();

    private AstNode ParseTernary()
    {
        var node = ParseLogicalOr();
        SkipWhitespace();
        if (Match('?'))
        {
            var trueExpr = ParseExpression();
            Expect(':');
            var falseExpr = ParseExpression();
            return new TernaryNode(node, trueExpr, falseExpr) { Start = node.Start, Length = _pos - node.Start };
        }
        return node;
    }

    private AstNode ParseLogicalOr()
    {
        var node = ParseLogicalAnd();
        while (true)
        {
            SkipWhitespace();
            if (Match("||") || Match("or"))
            {
                var right = ParseLogicalAnd();
                node = new BinaryNode(node, "||", right) { Start = node.Start, Length = _pos - node.Start };
            }
            else break;
        }
        return node;
    }

    private AstNode ParseLogicalAnd()
    {
        var node = ParseEquality();
        while (true)
        {
            SkipWhitespace();
            if (Match("&&") || Match("and"))
            {
                var right = ParseEquality();
                node = new BinaryNode(node, "&&", right) { Start = node.Start, Length = _pos - node.Start };
            }
            else break;
        }
        return node;
    }

    private AstNode ParseEquality()
    {
        var node = ParseComparison();
        while (true)
        {
            SkipWhitespace();
            if (Match("==") || Match("eq"))
                node = new BinaryNode(node, "==", ParseComparison()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match("!=") || Match("ne"))
                node = new BinaryNode(node, "!=", ParseComparison()) { Start = node.Start, Length = _pos - node.Start };
            else break;
        }
        return node;
    }

    private AstNode ParseComparison()
    {
        var node = ParseAddSub();
        while (true)
        {
            SkipWhitespace();
            if (Match("<=") || Match("le"))
                node = new BinaryNode(node, "<=", ParseAddSub()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match(">=") || Match("ge"))
                node = new BinaryNode(node, ">=", ParseAddSub()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match("<") || Match("lt"))
                node = new BinaryNode(node, "<", ParseAddSub()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match(">") || Match("gt"))
                node = new BinaryNode(node, ">", ParseAddSub()) { Start = node.Start, Length = _pos - node.Start };
            else break;
        }
        return node;
    }

    private AstNode ParseAddSub()
    {
        var node = ParseMulDiv();
        while (true)
        {
            SkipWhitespace();
            if (Match('+'))
                node = new BinaryNode(node, "+", ParseMulDiv()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match('-'))
                node = new BinaryNode(node, "-", ParseMulDiv()) { Start = node.Start, Length = _pos - node.Start };
            else break;
        }
        return node;
    }

    private AstNode ParseMulDiv()
    {
        var node = ParseUnary();
        while (true)
        {
            SkipWhitespace();
            if (Match('*'))
                node = new BinaryNode(node, "*", ParseUnary()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match('/'))
                node = new BinaryNode(node, "/", ParseUnary()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match('%'))
                node = new BinaryNode(node, "%", ParseUnary()) { Start = node.Start, Length = _pos - node.Start };
            else if (Match('^'))
                node = new BinaryNode(node, "^", ParseUnary()) { Start = node.Start, Length = _pos - node.Start };
            else break;
        }
        return node;
    }

    private AstNode ParseUnary()
    {
        SkipWhitespace();
        if (Match('+')) return ParseUnary();
        if (Match('-')) return new UnaryNode("-", ParseUnary()) { Start = _pos - 1, Length = _pos - (_pos - 1) };
        if (Match("!") || Match("not")) return new UnaryNode("!", ParseUnary()) { Start = _pos - 1, Length = _pos - (_pos - 1) };
        return ParsePrimary();
    }

    private AstNode ParsePrimary()
    {
        SkipWhitespace();
        int start = _pos;

        if (Match('('))
        {
            var node = ParseExpression();
            SkipWhitespace();
            Expect(')');
            return node;
        }

        if (Match('['))
        {
            SkipWhitespace();
            int nameStart = _pos;
            var sb = new System.Text.StringBuilder();
            while (_pos < _length && _source[_pos] != ']' && !char.IsWhiteSpace(_source[_pos]))
                sb.Append(_source[_pos++]);
            string mname = sb.ToString().Trim();
            SkipWhitespace();
            Expect(']');
            return new MeasureRefNode(mname) { Start = start, Length = _pos - start };
        }

        if (char.IsDigit(Peek()) || (Peek() == '.' && char.IsDigit(PeekNext())))
            return ParseNumber();

        if (Peek() == '"' || Peek() == '\'')
            return ParseString();

        if (char.IsLetter(Peek()) || Peek() == '_')
            return ParseIdentifierOrCall();

        throw new FormatException($"Unexpected character at position {_pos}: '{Peek()}'");
    }

    private AstNode ParseNumber()
    {
        int start = _pos;
        bool hasDot = false;
        while (char.IsDigit(Peek()) || (Peek() == '.' && !hasDot))
        {
            if (Peek() == '.') hasDot = true;
            _pos++;
        }
        string text = _source[start.._pos];
        double value = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        return new LiteralNode(FormulaValue.FromNumber(value)) { Start = start, Length = _pos - start };
    }

    private AstNode ParseString()
    {
        char quote = _source[_pos++];
        int start = _pos - 1;
        var sb = new System.Text.StringBuilder();
        while (_pos < _length && _source[_pos] != quote)
        {
            if (_source[_pos] == '\\' && _pos + 1 < _length)
            {
                _pos++;
                sb.Append(_source[_pos++] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '\\' => '\\', '"' => '"', '\'' => '\'', _ => _source[_pos - 1] });
            }
            else sb.Append(_source[_pos++]);
        }
        Expect(quote);
        return new LiteralNode(FormulaValue.FromString(sb.ToString())) { Start = start, Length = _pos - start };
    }

    private AstNode ParseIdentifierOrCall()
    {
        int start = _pos;
        var sb = new System.Text.StringBuilder();
        while (_pos < _length && (char.IsLetterOrDigit(Peek()) || Peek() == '_')) sb.Append(_source[_pos++]);
        string name = sb.ToString();

        SkipWhitespace();
        if (Match('('))
        {
            var args = new List<AstNode>();
            SkipWhitespace();
            if (!Match(')'))
            {
                while (true)
                {
                    args.Add(ParseExpression());
                    SkipWhitespace();
                    if (Match(')')) break;
                    Expect(',');
                    SkipWhitespace();
                }
            }
            return new CallNode(name, args) { Start = start, Length = _pos - start };
        }

        if (name.Equals("true", StringComparison.OrdinalIgnoreCase))
            return new LiteralNode(FormulaValue.FromBool(true)) { Start = start, Length = _pos - start };
        if (name.Equals("false", StringComparison.OrdinalIgnoreCase))
            return new LiteralNode(FormulaValue.FromBool(false)) { Start = start, Length = _pos - start };

        return new VariableNode(name) { Start = start, Length = _pos - start };
    }

    private void SkipWhitespace()
    {
        while (_pos < _length && char.IsWhiteSpace(Peek())) _pos++;
    }

    private bool Match(char c)
    {
        SkipWhitespace();
        if (_pos < _length && _source[_pos] == c) { _pos++; return true; }
        return false;
    }

    private bool Match(string s)
    {
        SkipWhitespace();
        if (_pos + s.Length <= _length && _source[_pos..(_pos + s.Length)].Equals(s, StringComparison.OrdinalIgnoreCase))
        {
            _pos += s.Length;
            return true;
        }
        return false;
    }

    private void Expect(char c)
    {
        SkipWhitespace();
        if (_pos >= _length || _source[_pos] != c) throw new FormatException($"Expected '{c}' at position {_pos}");
        _pos++;
    }

    private char Peek() => _pos < _length ? _source[_pos] : '\0';
    private char PeekNext() => _pos + 1 < _length ? _source[_pos + 1] : '\0';
}