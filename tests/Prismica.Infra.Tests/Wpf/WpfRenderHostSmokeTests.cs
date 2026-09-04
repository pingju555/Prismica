using System;
using System.Collections.Generic;
using System.Threading;
using Prismica.Core.Components;
using Prismica.Core.Formula;
using Prismica.Core.Parameters;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Meters;
using Prismica.Infra.Wpf;
using Xunit;

namespace Prismica.Infra.Tests.Wpf;

/// <summary>
/// Infra.Wpf 渲染宿主冒烟测试（WP3）：验证 CreateVisualRoot/HitTest/Arrange/Capture 纵向切片�?/// Capture/HitTest 需 STA（WPF），用专用线程执行�?/// </summary>
public class WpfRenderHostSmokeTests
{
    private static ComponentDefinition MakeDef()
    {
        var meter = new MeterDefinition("m1", "label", new Dictionary<string, string>
        {
            ["X"] = "10", ["Y"] = "10", ["W"] = "200", ["H"] = "50",
            ["Text"] = "Hello", ["Color"] = "#FF0000FF", ["FontSize"] = "16"
        });
        return new ComponentDefinition(
            "smoke", "0.1", new PrismicaSection("0.1", "smoke", "t", "test", 4, 30, 400, 300),
            new Dictionary<string, ArgbColor>(),
            new Dictionary<string, string>(),
            new ComponentParameterSchema("smoke", new Dictionary<string, ParameterInfo>()),
            new List<MeasureDefinition>(),
            new List<MeterDefinition> { meter },
            new List<EmbedDefinition>(),
            new List<StyleDefinition>(),
            new List<AnimationSpec>());
    }

    [Fact]
    public void CreateVisualRoot_SeedsMeterChildren()
    {
        int childCount = 0;
        double width = 0;
        RunOnSta(() =>
        {
            var host = new WpfRenderHost();
            var engine = new DefaultFormulaEngine();
            var ctx = new RenderContext(engine, new Dictionary<string, ArgbColor>(), 1.0, new Size(400, 300));
            var root = host.CreateVisualRoot(MakeDef(), ctx);
            Assert.IsType<WpfVisualRoot>(root);
            childCount = root.Children.Count;
            width = root.Definition.Prismica.Width;
        });
        Assert.Equal(1, childCount);
        Assert.Equal(400, width);
    }

    [Fact]
    public void HitTest_InsideMeterBounds_ReturnsHit()
    {
        bool hit = false, miss = true;
        RunOnSta(() =>
        {
            var host = new WpfRenderHost();
            var engine = new DefaultFormulaEngine();
            var ctx = new RenderContext(engine, new Dictionary<string, ArgbColor>(), 1.0, new Size(400, 300));
            var root = host.CreateVisualRoot(MakeDef(), ctx);
            hit = host.HitTest(root, new Point(20, 20)).Hit;
            miss = host.HitTest(root, new Point(500, 400)).Hit;
        });
        Assert.True(hit);
        Assert.False(miss);
    }

    [Fact]
    public void SetMeterText_UpdatesMeterText_ForLiveClock()
    {
        bool ok = false;
        string text = "";
        RunOnSta(() =>
        {
            var host = new WpfRenderHost();
            var engine = new DefaultFormulaEngine();
            var ctx = new RenderContext(engine, new Dictionary<string, ArgbColor>(), 1.0, new Size(400, 300));
            var root = host.CreateVisualRoot(MakeDef(), ctx);
            Assert.IsType<WpfVisualRoot>(root);
            var wpf = (WpfVisualRoot)root;
            ok = wpf.SetMeterText("m1", "12:34:56");
            text = wpf.Children[0] switch { WpfMeterVisual mv => mv.Text, _ => "" };
        });
        Assert.True(ok);
        Assert.Equal("12:34:56", text);
    }

    [Fact]
    public void HitTestContent_OnlyTrue_InsideMeterBounds()
    {
        bool inContent = false, inEmpty = false;
        RunOnSta(() =>
        {
            var host = new WpfRenderHost();
            var engine = new DefaultFormulaEngine();
            var ctx = new RenderContext(engine, new Dictionary<string, ArgbColor>(), 1.0, new Size(400, 300));
            var root = host.CreateVisualRoot(MakeDef(), ctx);
            Assert.IsType<WpfVisualRoot>(root);
            var wpf = (WpfVisualRoot)root;
            // MakeDef 的 meter m1 bounds: (10,10,200x50)
            inContent = wpf.HitTestContent(new Point(20, 20));
            inEmpty = wpf.HitTestContent(new Point(350, 250));
        });
        Assert.True(inContent);
        Assert.False(inEmpty);
    }

    [Fact]
    public void Capture_RendersPngBytes()
    {        RunOnSta(() =>
        {
            var host = new WpfRenderHost();
            var engine = new DefaultFormulaEngine();
            var ctx = new RenderContext(engine, new Dictionary<string, ArgbColor>(), 1.0, new Size(400, 300));
            var root = host.CreateVisualRoot(MakeDef(), ctx);
            host.ArrangeLayout(root, new Rect(0, 0, 400, 300));

            var bytes = host.CaptureAsync(root, ImageFormat.Png).GetAwaiter().GetResult();
            Assert.NotEmpty(bytes);
            Assert.Equal(0x89, bytes[0]); // PNG 魔数
        });
    }

    [Fact]
    public void RuntimeMeters_RenderAndHitTest_Works()
    {
        bool captured = false;
        bool inContent = false, inEmpty = true;
        byte firstByte = 0;
        RunOnSta(() =>
        {
            var host = new WpfRenderHost();
            var engine = new DefaultFormulaEngine();
            var ctx = new RenderContext(engine, new Dictionary<string, ArgbColor>(), 1.0, new Size(400, 300));
            var def = MakeDef();

            var clock = new StringMeter("clock");
            clock.Configure(new Dictionary<string, string> { ["X"] = "10", ["Y"] = "10", ["W"] = "200", ["H"] = "50" });
            clock.UpdateAsync(new MeterContext(new Dictionary<string, Prismica.Core.Measures.IMeasure>(), new Dictionary<string, ArgbColor>(), engine, new Rect(0, 0, 400, 300), TimeSpan.Zero, new Dictionary<string, string>())).GetAwaiter().GetResult();

            var root = new WpfVisualRoot(def, ctx, new System.Collections.Generic.List<IMeter> { clock });
            host.ArrangeLayout(root, new Rect(0, 0, 400, 300));

            var bytes = host.CaptureAsync(root, ImageFormat.Png).GetAwaiter().GetResult();
            captured = bytes.Length > 0;
            firstByte = bytes.Length > 0 ? bytes[0] : (byte)0;

            inContent = root.HitTestContent(new Point(20, 20));   // 时钟内容区
            inEmpty = root.HitTestContent(new Point(350, 250));   // 空区
        });
        Assert.True(captured);
        Assert.Equal(0x89, firstByte);
        Assert.True(inContent);
        Assert.False(inEmpty);
    }

    private static void RunOnSta(Action action)
    {
        Exception? thrown = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { thrown = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (thrown is not null) throw new Xunit.Sdk.XunitException("STA 线程异常: " + thrown);
    }
}
