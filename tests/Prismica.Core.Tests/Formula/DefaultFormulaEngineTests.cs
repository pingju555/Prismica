using Prismica.Core.Formula;
using Xunit;
using FluentAssertions;

namespace Prismica.Core.Tests.Formula;

public class DefaultFormulaEngineTests
{
    private readonly DefaultFormulaEngine _engine = new();

    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("10 - 3", 7)]
    [InlineData("4 * 5", 20)]
    [InlineData("20 / 4", 5)]
    [InlineData("2 ^ 3", 8)]
    [InlineData("10 % 3", 1)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("2 * (3 + 4)", 14)]
    [InlineData("-5 + 10", 5)]
    [InlineData("3.14 + 2.86", 6)]
    public void Arithmetic_Operations_Work(string formula, double expected)
    {
        var ast = _engine.Parse(formula);
        var result = _engine.Evaluate(ast, EmptyContext());
        result.AsNumber().Should().BeApproximately(expected, 1e-10);
    }

    [Theory]
    [InlineData("1 == 1", true)]
    [InlineData("1 != 2", true)]
    [InlineData("2 < 3", true)]
    [InlineData("3 > 2", true)]
    [InlineData("2 <= 2", true)]
    [InlineData("3 >= 2", true)]
    [InlineData("1 < 2 and 3 > 1", true)]
    [InlineData("1 < 2 or 5 < 3", true)]
    [InlineData("not false", true)]
    public void Comparison_And_Logic_Operations_Work(string formula, bool expected)
    {
        var ast = _engine.Parse(formula);
        var result = _engine.Evaluate(ast, EmptyContext());
        result.AsBool().Should().Be(expected);
    }

    [Theory]
    [InlineData("if(1 > 2, 'a', 'b')", "b")]
    [InlineData("if(true, 'yes', 'no')", "yes")]
    [InlineData("iif(false, 1, 2)", 2)]
    public void Ternary_Conditional_Works(string formula, object expected)
    {
        var ast = _engine.Parse(formula);
        var result = _engine.Evaluate(ast, EmptyContext());
        if (expected is string s) result.AsString().Should().Be(s);
        else if (expected is double d) result.AsNumber().Should().Be(d);
    }

    [Theory]
    [InlineData("abs(-5)", 5)]
    [InlineData("ceil(3.2)", 4)]
    [InlineData("floor(3.8)", 3)]
    [InlineData("round(3.6)", 4)]
    [InlineData("sqrt(16)", 4)]
    [InlineData("min(3, 1, 5)", 1)]
    [InlineData("max(3, 1, 5)", 5)]
    [InlineData("clamp(5, 1, 3)", 3)]
    [InlineData("lerp(0, 10, 0.5)", 5)]
    [InlineData("sin(0)", 0)]
    [InlineData("cos(0)", 1)]
    [InlineData("pow(2, 3)", 8)]
    public void Math_Functions_Work(string formula, double expected)
    {
        var ast = _engine.Parse(formula);
        var result = _engine.Evaluate(ast, EmptyContext());
        result.AsNumber().Should().BeApproximately(expected, 1e-10);
    }

    [Theory]
    [InlineData("strlen('hello')", 5)]
    [InlineData("upper('abc')", "ABC")]
    [InlineData("lower('ABC')", "abc")]
    [InlineData("trim('  hi  ')", "hi")]
    [InlineData("replace('a b c', ' ', '-')", "a-b-c")]
    [InlineData("contains('hello world', 'world')", true)]
    [InlineData("startswith('hello', 'he')", true)]
    [InlineData("endswith('hello', 'lo')", true)]
    public void String_Functions_Work(string formula, object expected)
    {
        var ast = _engine.Parse(formula);
        var result = _engine.Evaluate(ast, EmptyContext());
        if (expected is string s) result.AsString().Should().Be(s);
        else if (expected is bool b) result.AsBool().Should().Be(b);
        else if (expected is double d) result.AsNumber().Should().Be(d);
    }

    [Fact]
    public void Variable_Reference_Works()
    {
        var ctx = new EvalContext(
            new Dictionary<string, FormulaValue> { ["x"] = FormulaValue.FromNumber(10) },
            new Dictionary<string, Prismica.Core.Measures.IMeasure>(),
            new Dictionary<string, object>(),
            CancellationToken.None
        );
        var ast = _engine.Parse("x * 2");
        var result = _engine.Evaluate(ast, ctx);
        result.AsNumber().Should().Be(20);
    }

    [Fact]
    public void Measure_Reference_Works()
    {
        var mockMeasure = new MockMeasure(42);
        var ctx = new EvalContext(
            new Dictionary<string, FormulaValue>(),
            new Dictionary<string, Prismica.Core.Measures.IMeasure> { ["cpu"] = mockMeasure },
            new Dictionary<string, object>(),
            CancellationToken.None
        );
        var ast = _engine.Parse("[cpu] + 10");
        var result = _engine.Evaluate(ast, ctx);
        result.AsNumber().Should().Be(52);
    }

    [Fact]
    public void AST_Caching_Works()
    {
        var ast1 = _engine.Parse("1 + 2");
        var ast2 = _engine.Parse("1 + 2");
        ast1.Should().BeSameAs(ast2); // 同一实例
    }

    private static EvalContext EmptyContext() => new(
        new Dictionary<string, FormulaValue>(),
        new Dictionary<string, Prismica.Core.Measures.IMeasure>(),
        new Dictionary<string, object>(),
        CancellationToken.None
    );

    private sealed class MockMeasure : Prismica.Core.Measures.IMeasure
    {
        private readonly double _value;
        public MockMeasure(double v) => _value = v;
        public string Name => "mock";
        public Prismica.Core.Measures.MeasureTypeInfo TypeInfo => throw new NotImplementedException();
        public Prismica.Core.Measures.MeasureValue CurrentValue => new(_value, null, null);
#pragma warning disable CS0067 // 接口必需的 mock 事件，未使用
        public event Action<Prismica.Core.Measures.MeasureValue>? ValueChanged;
#pragma warning restore CS0067
        public ValueTask UpdateAsync(Prismica.Core.Measures.MeasureContext ctx, CancellationToken ct = default) => ValueTask.CompletedTask;
        public void Configure(IReadOnlyDictionary<string, string> fields) { }
        public void Dispose() { }
    }
}