using System.Collections.Generic;
using System.Linq;
using Prismica.Core.Formula;
using Xunit;

namespace Prismica.Core.Tests.Formula;

/// <summary>
/// #21 公式编辑器的纯逻辑契约：函数目录、语法校验、Formula= 字段序列化。
/// 这是沙箱内可验证的核心；Studio UI 只做绑定。
/// </summary>
public sealed class FormulaEditorLogicTests
{
    [Fact]
    public void Catalog_ContainsAllBuiltins_WithArgs()
    {
        Assert.Contains(FormulaCatalog.All, f => f.Name == "clamp");
        Assert.Contains(FormulaCatalog.All, f => f.Name == "lerp");
        Assert.Contains(FormulaCatalog.All, f => f.Name == "substr");

        var clamp = FormulaCatalog.All.Single(f => f.Name == "clamp");
        Assert.Equal(3, clamp.MinArgs);
        Assert.Equal(3, clamp.MaxArgs);
        Assert.Equal(new[] { "value", "min", "max" }, clamp.ParamNames);

        var min = FormulaCatalog.All.Single(f => f.Name == "min");
        Assert.Equal(1, min.MinArgs);
        Assert.Equal(int.MaxValue, min.MaxArgs);
    }

    [Fact]
    public void Validator_AcceptsValidFormula()
    {
        var d = FormulaValidator.Validate("[Cpu] * 100 + abs(-3)");
        Assert.Empty(d);
    }

    [Fact]
    public void Validator_ReportsPositionOnError()
    {
        var d = FormulaValidator.Validate("1 + ");
        Assert.Single(d);
        Assert.True(d[0].Start >= 0);
        Assert.Contains("position", d[0].Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_AcceptsEmptyAsValid()
    {
        Assert.Empty(FormulaValidator.Validate(""));
        Assert.Empty(FormulaValidator.Validate("   "));
    }

    [Fact]
    public void Serializer_ExtractsFormulaFields()
    {
        const string pri = "[Prismica]\nName=X\n\n[MeasureCalc]\nMeasure=Calc\nFormula=[Cpu] * 2\nUpdateDivider=1\n";
        var fields = FormulaFieldSerializer.Extract(pri);
        Assert.Single(fields);
        Assert.Equal("MeasureCalc", fields[0].Section);
        Assert.Equal("[Cpu] * 2", fields[0].Formula);
    }

    [Fact]
    public void Serializer_Apply_ReplacesFormulaPreservingRest()
    {
        const string pri = "[Prismica]\nName=X\n\n[MeasureCalc]\nMeasure=Calc\nFormula=[Cpu] * 2\nUpdateDivider=1\n";
        var applied = FormulaFieldSerializer.Apply(pri, new List<FormulaField> { new("MeasureCalc", "[Mem] + 1") });

        Assert.Contains("Formula=[Mem] + 1", applied);
        Assert.Contains("UpdateDivider=1", applied);
        Assert.Contains("[Prismica]", applied);

        var re = FormulaFieldSerializer.Extract(applied);
        Assert.Equal("[Mem] + 1", re[0].Formula);
    }

    [Fact]
    public void Serializer_Apply_AppendsWhenMissing()
    {
        const string pri = "[MeasureCalc]\nMeasure=Calc\nUpdateDivider=1\n";
        var applied = FormulaFieldSerializer.Apply(pri, new List<FormulaField> { new("MeasureCalc", "42") });
        Assert.Contains("Formula=42", applied);
        Assert.Contains("UpdateDivider=1", applied);
    }

    [Fact]
    public void Engine_GetFunctions_ReturnsCatalogMetadata()
    {
        var engine = new DefaultFormulaEngine();
        var funcs = engine.GetFunctions();
        Assert.True(funcs.TryGetValue("clamp", out var info));
        Assert.Equal(3, info.MinArgs);
        Assert.Equal(3, info.MaxArgs);
    }
}
