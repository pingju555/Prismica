namespace Prismica.Core.Formula;

public sealed class DefaultFormulaEngine : IFormulaEngine
{
    private readonly Dictionary<string, FormulaFunction> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FormulaAst> _astCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    public DefaultFormulaEngine()
    {
        RegisterBuiltins();
    }

    public FormulaAst Parse(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return EmptyAst();

        lock (_cacheLock)
        {
            if (_astCache.TryGetValue(formula, out var cached)) return cached;
        }

        var parser = new FormulaParser(formula);
        var ast = parser.Parse();

        lock (_cacheLock)
        {
            _astCache[formula] = ast;
        }
        return ast;
    }

    public FormulaValue Evaluate(FormulaAst ast, EvalContext ctx)
    {
        return EvaluateNode(ast.Root, ctx);
    }

    public void RegisterFunction(string name, FormulaFunction func)
    {
        _functions[name] = func;
    }

    public IReadOnlyDictionary<string, FunctionInfo> GetFunctions()
    {
        var result = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in _functions.Keys)
        {
            FunctionInfo info = FormulaCatalog.TryGet(name, out var cat) && cat is not null
                ? cat
                : new FunctionInfo(name, 0, int.MaxValue, "", Array.Empty<string>());
            result[name] = info;
        }
        return result;
    }

    private FormulaValue EvaluateNode(AstNode node, EvalContext ctx)
    {
        return node switch
        {
            LiteralNode lit => lit.Value,
            VariableNode var => ctx.Variables.TryGetValue(var.Name, out var v) ? v : FormulaValue.FromNumber(0),
            MeasureRefNode mref => ctx.Measures.TryGetValue(mref.Name, out var m) ? m.CurrentValue : FormulaValue.FromNumber(0),
            UnaryNode u => EvaluateUnary(u.Op, EvaluateNode(u.Operand, ctx)),
            BinaryNode b => EvaluateBinary(b.Op, EvaluateNode(b.Left, ctx), EvaluateNode(b.Right, ctx)),
            CallNode c => EvaluateCall(c.Name, c.Args, ctx),
            TernaryNode t => EvaluateNode(t.Condition, ctx).AsBool() ? EvaluateNode(t.TrueExpr, ctx) : EvaluateNode(t.FalseExpr, ctx),
            _ => FormulaValue.FromNumber(0)
        };
    }

    private FormulaValue EvaluateUnary(string op, FormulaValue val)
    {
        return op switch
        {
            "-" => FormulaValue.FromNumber(-val.AsNumber()),
            "+" => val,
            "!" or "not" => FormulaValue.FromBool(!val.AsBool()),
            _ => FormulaValue.FromNumber(0)
        };
    }

    private FormulaValue EvaluateBinary(string op, FormulaValue left, FormulaValue right)
    {
        double l = left.AsNumber(), r = right.AsNumber();
        return op switch
        {
            "+" => FormulaValue.FromNumber(l + r),
            "-" => FormulaValue.FromNumber(l - r),
            "*" => FormulaValue.FromNumber(l * r),
            "/" => r != 0 ? FormulaValue.FromNumber(l / r) : FormulaValue.FromNumber(double.NaN),
            "%" => r != 0 ? FormulaValue.FromNumber(l % r) : FormulaValue.FromNumber(double.NaN),
            "^" => FormulaValue.FromNumber(Math.Pow(l, r)),
            "==" or "eq" => FormulaValue.FromBool(Math.Abs(l - r) < 1e-10),
            "!=" or "ne" => FormulaValue.FromBool(Math.Abs(l - r) >= 1e-10),
            "<" or "lt" => FormulaValue.FromBool(l < r),
            ">" or "gt" => FormulaValue.FromBool(l > r),
            "<=" or "le" => FormulaValue.FromBool(l <= r),
            ">=" or "ge" => FormulaValue.FromBool(l >= r),
            "and" or "&&" => FormulaValue.FromBool(left.AsBool() && right.AsBool()),
            "or" or "||" => FormulaValue.FromBool(left.AsBool() || right.AsBool()),
            _ => FormulaValue.FromNumber(0)
        };
    }

    private FormulaValue EvaluateCall(string name, IReadOnlyList<AstNode> args, EvalContext ctx)
    {
        if (!_functions.TryGetValue(name, out var func))
            return FormulaValue.FromNumber(double.NaN);

        var evaluatedArgs = args.Select(a => EvaluateNode(a, ctx)).ToList();
        try
        {
            return func(evaluatedArgs);
        }
        catch
        {
            return FormulaValue.FromNumber(double.NaN);
        }
    }

    private void RegisterBuiltins()
    {
        Register("abs", args => FormulaValue.FromNumber(Math.Abs(args[0].AsNumber())));
        Register("ceil", args => FormulaValue.FromNumber(Math.Ceiling(args[0].AsNumber())));
        Register("floor", args => FormulaValue.FromNumber(Math.Floor(args[0].AsNumber())));
        Register("round", args => FormulaValue.FromNumber(Math.Round(args[0].AsNumber())));
        Register("sqrt", args => FormulaValue.FromNumber(Math.Sqrt(Math.Max(0, args[0].AsNumber()))));
        Register("min", args => FormulaValue.FromNumber(args.Min(a => a.AsNumber())));
        Register("max", args => FormulaValue.FromNumber(args.Max(a => a.AsNumber())));
        Register("clamp", args => FormulaValue.FromNumber(Math.Clamp(args[0].AsNumber(), args[1].AsNumber(), args[2].AsNumber())));
        Register("lerp", args => FormulaValue.FromNumber(args[0].AsNumber() + (args[1].AsNumber() - args[0].AsNumber()) * args[2].AsNumber()));
        Register("sin", args => FormulaValue.FromNumber(Math.Sin(args[0].AsNumber())));
        Register("cos", args => FormulaValue.FromNumber(Math.Cos(args[0].AsNumber())));
        Register("tan", args => FormulaValue.FromNumber(Math.Tan(args[0].AsNumber())));
        Register("asin", args => FormulaValue.FromNumber(Math.Asin(args[0].AsNumber())));
        Register("acos", args => FormulaValue.FromNumber(Math.Acos(args[0].AsNumber())));
        Register("atan", args => FormulaValue.FromNumber(Math.Atan(args[0].AsNumber())));
        Register("atan2", args => FormulaValue.FromNumber(Math.Atan2(args[0].AsNumber(), args[1].AsNumber())));
        Register("rad", args => FormulaValue.FromNumber(args[0].AsNumber() * Math.PI / 180));
        Register("deg", args => FormulaValue.FromNumber(args[0].AsNumber() * 180 / Math.PI));
        Register("log", args => FormulaValue.FromNumber(Math.Log(args[0].AsNumber())));
        Register("log10", args => FormulaValue.FromNumber(Math.Log10(args[0].AsNumber())));
        Register("exp", args => FormulaValue.FromNumber(Math.Exp(args[0].AsNumber())));
        Register("pow", args => FormulaValue.FromNumber(Math.Pow(args[0].AsNumber(), args[1].AsNumber())));

        Register("substr", args => FormulaValue.FromString(args[0].AsString().Substring((int)args[1].AsNumber(), args.Count > 2 ? (int)args[2].AsNumber() : int.MaxValue)));
        Register("strlen", args => FormulaValue.FromNumber(args[0].AsString().Length));
        Register("upper", args => FormulaValue.FromString(args[0].AsString().ToUpperInvariant()));
        Register("lower", args => FormulaValue.FromString(args[0].AsString().ToLowerInvariant()));
        Register("trim", args => FormulaValue.FromString(args[0].AsString().Trim()));
        Register("replace", args => FormulaValue.FromString(args[0].AsString().Replace(args[1].AsString(), args[2].AsString())));
        Register("contains", args => FormulaValue.FromBool(args[0].AsString().Contains(args[1].AsString(), StringComparison.OrdinalIgnoreCase)));
        Register("startswith", args => FormulaValue.FromBool(args[0].AsString().StartsWith(args[1].AsString(), StringComparison.OrdinalIgnoreCase)));
        Register("endswith", args => FormulaValue.FromBool(args[0].AsString().EndsWith(args[1].AsString(), StringComparison.OrdinalIgnoreCase)));

        Register("if", args => args[0].AsBool() ? args[1] : args[2]);
        Register("iif", args => args[0].AsBool() ? args[1] : args[2]);

        Register("time", args => FormulaValue.FromString(DateTime.Now.ToString(args.Count > 0 ? args[0].AsString() : "HH:mm:ss")));
        Register("timestamp", args => FormulaValue.FromNumber(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    private void Register(string name, FormulaFunction func)
    {
        _functions[name] = func;
    }

    private static FormulaAst EmptyAst() => new() { Root = new LiteralNode(FormulaValue.FromNumber(0)), Source = "", Length = 0 };
}