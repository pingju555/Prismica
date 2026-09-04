using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prismica.App;
using Prismica.Core.Components;
using Prismica.Core.Formula;
using Prismica.Core.Meters;
using Prismica.Core.Parameters;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Xunit;
using FluentAssertions;

namespace Prismica.Infra.Tests.App;

public class ComponentRuntimeTests
{
    private static ComponentDefinition BuildDef()
        => new(
            "TestClock", "0.1",
            new PrismicaSection("0.1", "TestClock", "tester", "demo", 4, 1000, 240, 120),
            new Dictionary<string, ArgbColor>(),
            new Dictionary<string, string>(),
            new ComponentParameterSchema("TestClock", new Dictionary<string, ParameterInfo>()),
            new List<MeasureDefinition>
            {
                new("Time", "Time", new Dictionary<string, string> { ["Format"] = "%H:%M:%S" }),
                new("Cpu", "CPU", new Dictionary<string, string>()),
            },
            new List<MeterDefinition>
            {
                new("Clock", "String", new Dictionary<string, string> { ["MeasureName"] = "Time", ["X"] = "10", ["Y"] = "10", ["W"] = "180", ["H"] = "30" }),
                new("Load", "Progress", new Dictionary<string, string> { ["MeasureName"] = "Cpu", ["X"] = "10", ["Y"] = "50", ["W"] = "200", ["H"] = "12" }),
            },
            new List<EmbedDefinition>(),
            new List<StyleDefinition>(),
            new List<AnimationSpec>());

    private sealed class RecordingCtx : IRenderContext
    {
        public int TextCount;
        public int RoundedRectCount;
        public Rect ClipBounds => Rect.Empty;
        public double DpiScale => 1.0;
        public void DrawText(string text, Point position, ArgbColor color, string fontFamily, double fontSize, FontWeight weight = FontWeight.Normal) => TextCount++;
        public void DrawRect(Rect rect, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1) { }
        public void DrawRoundedRect(Rect rect, CornerRadius radius, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1) => RoundedRectCount++;
        public void DrawEllipse(Rect rect, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1) { }
        public void DrawLine(Point p1, Point p2, ArgbColor color, double thickness = 1) { }
        public void DrawPath(GeometryPath path, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1) { }
        public void DrawImage(IImage image, Rect destRect, Rect? srcRect = null, double opacity = 1) { }
        public void PushClip(Rect clip) { }
        public void PopClip() { }
        public void PushOpacity(double opacity) { }
        public void PopOpacity() { }
        public void PushTransform(Transform transform) { }
        public void PopTransform() { }
    }

    [Fact]
    public void Create_InstantiatesMeasuresAndMeters()
    {
        var def = BuildDef();
        using var rt = ComponentRuntime.Create(def, new DefaultFormulaEngine());

        rt.Measures.Should().ContainKey("Time");
        rt.Measures.Should().ContainKey("Cpu");
        rt.Meters.Should().HaveCount(2);
        rt.Meters[0].Should().BeOfType<StringMeter>();
        rt.Meters[1].Should().BeOfType<ProgressMeter>();
        rt.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ResolvesMeasureIntoMeterText()
    {
        var def = BuildDef();
        using var rt = ComponentRuntime.Create(def, new DefaultFormulaEngine());

        await rt.UpdateAsync(TimeSpan.FromMilliseconds(16));

        var clock = (StringMeter)rt.Meters[0];
        clock.RenderedText.Should().NotBeNullOrEmpty();
        clock.Layout.Width.Should().Be(180);
        var load = (ProgressMeter)rt.Meters[1];
        load.BoundMeasureNames.Should().Contain("Cpu");
    }

    [Fact]
    public void UnknownKeyword_CollectedAsDiagnosticNotCrash()
    {
        var def = new ComponentDefinition(
            "Bad", "0.1",
            new PrismicaSection("0.1", "Bad", "", "", 4, 1000, 100, 100),
            new Dictionary<string, ArgbColor>(),
            new Dictionary<string, string>(),
            new ComponentParameterSchema("Bad", new Dictionary<string, ParameterInfo>()),
            new List<MeasureDefinition> { new("M", "Nope", new Dictionary<string, string>()) },
            new List<MeterDefinition> { new("V", "Missing", new Dictionary<string, string>()) },
            new List<EmbedDefinition>(),
            new List<StyleDefinition>(),
            new List<AnimationSpec>());

        using var rt = ComponentRuntime.Create(def, new DefaultFormulaEngine());

        rt.Diagnostics.Keys.Should().Contain("Measure:M");
        rt.Diagnostics.Keys.Should().Contain("Meter:V");
        rt.Measures.Should().BeEmpty();
        rt.Meters.Should().BeEmpty();
    }

    [Fact]
    public void Create_AppliesMeterStyleInheritance()
    {
        // [MeterStyleTitle] 定义命名样式；meter 引用并覆盖 FontColor —— 运行时 meter.Style 应反映合并结果。
        var def = new ComponentDefinition(
            "Styled", "0.1",
            new PrismicaSection("0.1", "Styled", "", "", 4, 1000, 200, 100),
            new Dictionary<string, ArgbColor>(),
            new Dictionary<string, string>(),
            new ComponentParameterSchema("Styled", new Dictionary<string, ParameterInfo>()),
            new List<MeasureDefinition>(),
            new List<MeterDefinition>
            {
                new("Title", "String", new Dictionary<string, string>
                {
                    ["MeterStyle"] = "Title",
                    ["X"] = "0", ["Y"] = "0", ["W"] = "200", ["H"] = "40",
                    ["Text"] = "Hi",
                    ["FontColor"] = "#FF00FF00", // 覆盖样式
                }),
            },
            new List<EmbedDefinition>(),
            new List<StyleDefinition>
            {
                new("Title", new Dictionary<string, string> { ["FontColor"] = "#FFFF0000", ["FontSize"] = "22" }),
            },
            new List<AnimationSpec>());

        using var rt = ComponentRuntime.Create(def, new DefaultFormulaEngine());

        rt.Meters.Should().HaveCount(1);
        var meter = rt.Meters[0];
        meter.Style.Should().NotBeNull();
        meter.Style!.ParentStyles.Should().Contain("Title");
        // 样式 FontSize 被继承，meter 自身 FontColor 覆盖样式。
        meter.Style.Fields["FontSize"].Should().Be("22");
        meter.Style.Fields["FontColor"].Should().Be("#FF00FF00");
        // 引用键不进入合并字段。
        meter.Style.Fields.Should().NotContainKey("MeterStyle");
        rt.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Create_UnknownStyleRef_ReportedAsDiagnostic()
    {
        var def = new ComponentDefinition(
            "Styled", "0.1",
            new PrismicaSection("0.1", "Styled", "", "", 4, 1000, 200, 100),
            new Dictionary<string, ArgbColor>(),
            new Dictionary<string, string>(),
            new ComponentParameterSchema("Styled", new Dictionary<string, ParameterInfo>()),
            new List<MeasureDefinition>(),
            new List<MeterDefinition>
            {
                new("Title", "String", new Dictionary<string, string> { ["MeterStyle"] = "Ghost", ["X"] = "0", ["Y"] = "0", ["W"] = "200", ["H"] = "40" }),
            },
            new List<EmbedDefinition>(),
            new List<StyleDefinition>(),
            new List<AnimationSpec>());

        using var rt = ComponentRuntime.Create(def, new DefaultFormulaEngine());

        // 缺失样式引用被记录为诊断，但 meter 仍正常构造。
        rt.Diagnostics.Keys.Should().Contain("Meter:Title");
        rt.Diagnostics["Meter:Title"].Should().Contain("Ghost");
        rt.Meters.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_CalcMeasureEvaluatesWithoutNullReference()
    {
        // 回归锁：此前 MeasureContext 第 3 参(FormulaEngine)写死为 null!，
        // 导致 Calc 度量在 UpdateAsync 中 NPE 崩溃（真实桌面运行必崩）。
        // 修复后引擎经 ComponentRuntime._engine 传入，Calc 应正常求值。
        var def = new ComponentDefinition(
            "Calc", "0.1",
            new PrismicaSection("0.1", "Calc", "", "", 4, 1000, 200, 100),
            new Dictionary<string, ArgbColor>(),
            new Dictionary<string, string>(),
            new ComponentParameterSchema("Calc", new Dictionary<string, ParameterInfo>()),
            new List<MeasureDefinition>
            {
                new("C", "Calc", new Dictionary<string, string> { ["Formula"] = "1 + 2 * 3" }),
            },
            new List<MeterDefinition>(),
            new List<EmbedDefinition>(),
            new List<StyleDefinition>(),
            new List<AnimationSpec>());

        using var rt = ComponentRuntime.Create(def, new DefaultFormulaEngine());

        var act = async () => await rt.UpdateAsync(TimeSpan.Zero);

        await act.Should().NotThrowAsync();
        rt.Measures.Should().ContainKey("C");
        rt.Measures["C"].CurrentValue.Number.Should().Be(7d);
    }
}
