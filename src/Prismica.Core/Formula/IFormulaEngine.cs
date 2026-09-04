using System;
using System.Collections.Generic;
using Prismica.Core.Measures;
using Prismica.Core.Primitives;

namespace Prismica.Core.Formula;

public interface IFormulaEngine
{
    FormulaAst Parse(string formula);
    FormulaValue Evaluate(FormulaAst ast, EvalContext ctx);
    void RegisterFunction(string name, FormulaFunction func);
    IReadOnlyDictionary<string, FunctionInfo> GetFunctions();
}

public delegate FormulaValue FormulaFunction(IReadOnlyList<FormulaValue> args);

public sealed record FunctionInfo(
    string Name,
    int MinArgs,
    int MaxArgs,
    string Description,
    IReadOnlyList<string> ParamNames
);

public sealed record EvalContext(
    IReadOnlyDictionary<string, FormulaValue> Variables,
    IReadOnlyDictionary<string, IMeasure> Measures,
    IReadOnlyDictionary<string, object> ContextObjects,
    CancellationToken CancellationToken
);

public sealed record FormulaAst
{
    public required AstNode Root { get; init; }
    public required string Source { get; init; }
    public int Length { get; init; }
}

public abstract record AstNode
{
    public int Start { get; init; }
    public int Length { get; init; }
}

public sealed record BinaryNode(AstNode Left, string Op, AstNode Right) : AstNode;
public sealed record UnaryNode(string Op, AstNode Operand) : AstNode;
public sealed record CallNode(string Name, IReadOnlyList<AstNode> Args) : AstNode;
public sealed record VariableNode(string Name) : AstNode;
public sealed record MeasureRefNode(string Name) : AstNode;
public sealed record LiteralNode(FormulaValue Value) : AstNode;
public sealed record TernaryNode(AstNode Condition, AstNode TrueExpr, AstNode FalseExpr) : AstNode;

public readonly record struct FormulaValue
{
    public double? Number { get; init; }
    public string? String { get; init; }
    public bool? Boolean { get; init; }
    public IReadOnlyList<FormulaValue>? List { get; init; }

    public static FormulaValue FromNumber(double n) => new() { Number = n };
    public static FormulaValue FromString(string s) => new() { String = s };
    public static FormulaValue FromBool(bool b) => new() { Boolean = b };
    public static FormulaValue FromList(IReadOnlyList<FormulaValue> l) => new() { List = l };

    // Implicit conversion from MeasureValue
    public static implicit operator FormulaValue(Prismica.Core.Measures.MeasureValue mv)
    {
        if (mv.Number.HasValue) return FromNumber(mv.Number.Value);
        if (mv.String != null) return FromString(mv.String);
        if (mv.List != null) return FromList(mv.List.Select(v => v switch
        {
            double d => FromNumber(d),
            string s => FromString(s),
            bool b => FromBool(b),
            _ => FromNumber(0)
        }).ToList());
        return new();
    }

    public bool IsEmpty => Number == null && String == null && Boolean == null && List == null;

    public double AsNumber() => Number ?? 0;
    public string AsString() => String ?? Number?.ToString() ?? Boolean?.ToString() ?? "";
    public bool AsBool() => Boolean ?? Number != 0 || !string.IsNullOrEmpty(String);

    public override string ToString() => AsString();
}