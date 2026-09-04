using System.Collections.Generic;
using Prismica.Core.Formula;
using Xunit;

namespace Prismica.Core.Tests.Formula;

public class FormulaValidationTests
{
    [Fact]
    public void Validate_ValidFormula_NoDiagnostics()
    {
        Assert.Empty(FormulaValidator.Validate("1 + 2"));
        Assert.Empty(FormulaValidator.Validate("[Cpu] * 0.5"));
    }

    [Fact]
    public void Validate_Empty_ReturnsEmpty()
    {
        Assert.Empty(FormulaValidator.Validate(""));
        Assert.Empty(FormulaValidator.Validate("   "));
    }

    [Fact]
    public void Validate_SyntaxError_HasPosition()
    {
        var diags = FormulaValidator.Validate("1 +");
        Assert.Single(diags);
        Assert.True(diags[0].Start >= 0);
        Assert.True(diags[0].Length >= 0);
    }

    [Fact]
    public void FieldSerializer_Extract_OnlyMeasureFormula()
    {
        const string pri = """
[MeasureCpu]
Formula=[CpuLoad]

[MeterBox]
Meter=String
Text=Hi
""";
        var fields = FormulaFieldSerializer.Extract(pri);
        Assert.Single(fields);
        Assert.Equal("MeasureCpu", fields[0].Section);
        Assert.Equal("[CpuLoad]", fields[0].Formula);
    }

    [Fact]
    public void FieldSerializer_Apply_RewritesFormula()
    {
        const string pri = """
[MeasureCpu]
Formula=[Old]

[MeterBox]
Meter=String
Text=Hi
""";
        var fields = new List<FormulaField> { new("MeasureCpu", "[New]") };
        var applied = FormulaFieldSerializer.Apply(pri, fields);
        var re = FormulaFieldSerializer.Extract(applied);
        Assert.Equal("[New]", re[0].Formula);
        Assert.Contains("Text=Hi", applied);
    }

    [Fact]
    public void FieldSerializer_Apply_KeepsOtherLines()
    {
        const string pri = "[MeasureCpu]\nType=CPU\nFormula=[X]\n";
        var fields = new List<FormulaField> { new("MeasureCpu", "[Y]") };
        var applied = FormulaFieldSerializer.Apply(pri, fields);
        Assert.Contains("Type=CPU", applied);
        Assert.Contains("Formula=[Y]", applied);
        Assert.DoesNotContain("Formula=[X]", applied);
    }
}
