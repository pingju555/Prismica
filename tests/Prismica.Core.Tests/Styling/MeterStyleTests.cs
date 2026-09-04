using System.Collections.Generic;
using Prismica.Core.Components;
using Prismica.Core.Formula;
using Prismica.Core.Meters;
using Prismica.Core.Parsing;
using Prismica.Core.Styling;
using Xunit;

namespace Prismica.Core.Tests.Styling;

/// <summary>
/// MeterStyle 继承测试：纯逻辑解析（合并优先级 / 嵌套 / 环检测 / 缺失 / 大小写）+ 解析器→运行时端到端。
/// </summary>
public class MeterStyleTests
{
    private static StyleDefinition Style(string name, params (string k, string v)[] fields)
        => new(name, ToDict(fields));

    private static IReadOnlyDictionary<string, string> ToDict(params (string k, string v)[] fields)
    {
        var d = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in fields) d[k] = v;
        return d;
    }

    [Fact]
    public void Resolve_SingleStyle_MergesAndOverrides()
    {
        var styles = new List<StyleDefinition> { Style("Bold", ("FontColor", "#FFFF0000"), ("FontSize", "20")) };
        var meter = ToDict(("Meter", "String"), ("MeterStyle", "Bold"), ("X", "0"), ("FontColor", "#FF00FF00"));

        var r = MeterStyleResolver.Resolve(meter, styles);

        // 样式字段被继承
        Assert.Equal("20", r.MergedFields["FontSize"]);
        // meter 自身覆盖样式同名字段
        Assert.Equal("#FF00FF00", r.MergedFields["FontColor"]);
        // 引用键不进入合并结果
        Assert.False(r.MergedFields.ContainsKey("MeterStyle"));
        Assert.Equal(new[] { "Bold" }, r.ParentStyles);
        Assert.Empty(r.MissingStyles);
    }

    [Fact]
    public void Resolve_MultipleStyles_LaterOverridesEarlier_AndMeterWins()
    {
        var styles = new List<StyleDefinition>
        {
            Style("Base", ("FontColor", "#FFFF0000"), ("FontSize", "12")),
            Style("Child", ("FontSize", "20"), ("X", "5")),
        };
        var meter = ToDict(("Meter", "String"), ("MeterStyle", "Base,Child"), ("FontSize", "30"));

        var r = MeterStyleResolver.Resolve(meter, styles);

        // Base < Child < meter：FontSize 最终 30
        Assert.Equal("30", r.MergedFields["FontSize"]);
        // Child 的 X=5 覆盖 Base（Base 无 X）→ 保留
        Assert.Equal("5", r.MergedFields["X"]);
        // Base 的 FontColor 未被后续覆盖 → 保留
        Assert.Equal("#FFFF0000", r.MergedFields["FontColor"]);
        Assert.Equal(new[] { "Base", "Child" }, r.ParentStyles);
    }

    [Fact]
    public void Resolve_NestedStyle_InheritsGrandparent()
    {
        // Child 引用 Base；meter 引用 Child —— 应继承 Base 的字段。
        var styles = new List<StyleDefinition>
        {
            Style("Base", ("FontColor", "#FFFF0000")),
            Style("Child", ("MeterStyle", "Base"), ("FontSize", "20")),
        };
        var meter = ToDict(("Meter", "String"), ("MeterStyle", "Child"));

        var r = MeterStyleResolver.Resolve(meter, styles);

        Assert.Equal("#FFFF0000", r.MergedFields["FontColor"]); // 来自 Base（经 Child 嵌套）
        Assert.Equal("20", r.MergedFields["FontSize"]);          // 来自 Child
        Assert.Contains("Child", r.ParentStyles);
        Assert.Contains("Base", r.ParentStyles);
    }

    [Fact]
    public void Resolve_UnknownStyle_ReportedAndMeterFieldsStillApply()
    {
        var styles = new List<StyleDefinition> { Style("Real", ("FontColor", "#FFFF0000")) };
        var meter = ToDict(("Meter", "String"), ("MeterStyle", "Real,Missing"), ("X", "0"));

        var r = MeterStyleResolver.Resolve(meter, styles);

        Assert.Equal(new[] { "Missing" }, r.MissingStyles);
        Assert.Contains("Real", r.ParentStyles);
        // 已知样式字段仍继承，meter 自身字段仍生效
        Assert.Equal("#FFFF0000", r.MergedFields["FontColor"]);
        Assert.Equal("0", r.MergedFields["X"]);
    }

    [Fact]
    public void Resolve_Cycle_DoesNotInfiniteLoop()
    {
        // A 引用 B，B 引用 A —— 环检测应截断，不抛异常、不死循环。
        var styles = new List<StyleDefinition>
        {
            Style("A", ("MeterStyle", "B"), ("ColorA", "1")),
            Style("B", ("MeterStyle", "A"), ("ColorB", "2")),
        };
        var meter = ToDict(("Meter", "String"), ("MeterStyle", "A"));

        var r = MeterStyleResolver.Resolve(meter, styles);

        Assert.Equal("1", r.MergedFields["ColorA"]);
        Assert.Equal("2", r.MergedFields["ColorB"]);
    }

    [Fact]
    public void Resolve_NoStyleRef_MergedEqualsMeterFields()
    {
        var meter = ToDict(("Meter", "String"), ("X", "1"), ("FontColor", "#FFFFFFFF"));
        var r = MeterStyleResolver.Resolve(meter, new List<StyleDefinition>());

        Assert.Equal("1", r.MergedFields["X"]);
        Assert.Equal("#FFFFFFFF", r.MergedFields["FontColor"]);
        Assert.Empty(r.ParentStyles);
        Assert.Empty(r.MissingStyles);
    }

    [Fact]
    public void Resolve_CaseInsensitive_StyleName()
    {
        var styles = new List<StyleDefinition> { Style("bold", ("FontColor", "#FFFF0000")) };
        var meter = ToDict(("Meter", "String"), ("MeterStyle", "BOLD"));

        var r = MeterStyleResolver.Resolve(meter, styles);

        Assert.Equal("#FFFF0000", r.MergedFields["FontColor"]);
        Assert.Equal(new[] { "BOLD" }, r.ParentStyles);
    }

    [Fact]
    public void Parser_AndResolver_AppliesInheritedStyleToMeter()
    {
        // 端到端（纯 Core）：解析 .pri（含 [MeterStyle] + 引用它的 meter）→ 经解析器产出 + MeterStyleResolver 合并。
        var pri = @"[Prismica]
Name=StyleTest
[Variables]
Fall= #FFFFFFFF
[MeterStyleTitle]
FontColor=#FFFF0000
FontSize=22
[MeterTitle]
Meter=String
MeterStyle=Title
Text=Hi
FontColor=#FF00FF00
X=0
Y=0
W=200
H=40";
        var result = new IniSkinTextParser().Parse(pri);
        Assert.True(result.Success);
        Assert.NotNull(result.Definition);

        // [MeterStyleTitle] 被解析为样式，而不是被误当 meter（修复 ParseMeters 误吞 [MeterStyle*] 的 bug）。
        Assert.Contains(result.Definition!.Styles, s => s.Name == "Title");
        Assert.DoesNotContain(result.Definition.Meters, m => m.Name == "StyleTitle");

        var meter = result.Definition.Meters[0];
        var r = MeterStyleResolver.Resolve(meter.Fields, result.Definition.Styles);

        // 样式 FontSize 被继承，meter 自身 FontColor 覆盖样式；引用键被剥离。
        Assert.Equal("22", r.MergedFields["FontSize"]);
        Assert.Equal("#FF00FF00", r.MergedFields["FontColor"]);
        Assert.Equal(new[] { "Title" }, r.ParentStyles);
        Assert.False(r.MergedFields.ContainsKey("MeterStyle"));
    }
}
