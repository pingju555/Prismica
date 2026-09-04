using System.Collections.Generic;
using System.Threading.Tasks;
using Prismica.Core.Measures;
using Prismica.Core.Meters;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Xunit;
using FluentAssertions;

namespace Prismica.Core.Tests.Meters;

public class MeterRenderTests
{
    private static MeterContext Ctx(params IMeasure[] measures) => new(
        ToDict(measures), new Dictionary<string, ArgbColor>(), null!, Rect.Empty, System.TimeSpan.Zero, new Dictionary<string, string>());

    private static Dictionary<string, IMeasure> ToDict(IMeasure[] ms)
    {
        var d = new Dictionary<string, IMeasure>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var m in ms) d[m.Name] = m;
        return d;
    }

    private sealed class StubMeasure(string name, MeasureValue value) : IMeasure
    {
        public string Name { get; } = name;
        public MeasureTypeInfo TypeInfo => null!;
        public MeasureValue CurrentValue { get; private set; } = value;
        public event System.Action<MeasureValue>? ValueChanged;
        public ValueTask UpdateAsync(MeasureContext ctx, System.Threading.CancellationToken ct = default) => ValueTask.CompletedTask;
        public void Configure(IReadOnlyDictionary<string, string> fields) { }
        public void Set(MeasureValue v) { CurrentValue = v; ValueChanged?.Invoke(v); }
        public void Dispose() { }
    }

    private sealed class RecordingCtx : IRenderContext
    {
        public int TextCount;
        public int RoundedRectCount;
        public ArgbColor? LastTextColor;
        public string? LastText;
        public Rect ClipBounds => Rect.Empty;
        public double DpiScale => 1.0;
        public void DrawText(string text, Point position, ArgbColor color, string fontFamily, double fontSize, FontWeight weight = FontWeight.Normal)
        { TextCount++; LastText = text; LastTextColor = color; }
        public void DrawRect(Rect rect, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1) { }
        public void DrawRoundedRect(Rect rect, CornerRadius radius, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1) { RoundedRectCount++; }
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
    public async Task StringMeter_BoundMeasure_ResolvesValueText()
    {
        var measure = new StubMeasure("Time", MeasureValue.FromString("12:00:01"));
        var meter = new StringMeter("Display");
        meter.Configure(new Dictionary<string, string>
        {
            ["MeasureName"] = "Time",
            ["X"] = "10", ["Y"] = "20", ["W"] = "180", ["H"] = "30",
            ["FontColor"] = "#FF00FF00"
        });

        await meter.UpdateAsync(Ctx(measure));

        meter.RenderedText.Should().Be("12:00:01");
        meter.Layout.X.Should().Be(10);
        meter.Layout.Y.Should().Be(20);
        meter.Layout.Width.Should().Be(180);
        meter.Layout.Height.Should().Be(30);
    }

    [Fact]
    public void StringMeter_Configure_SetsLayoutFromFields()
    {
        var measure = new StubMeasure("Time", MeasureValue.FromString("x"));
        var meter = new StringMeter("Display");
        meter.Configure(new Dictionary<string, string>
        {
            ["X"] = "5", ["Y"] = "7", ["W"] = "120", ["H"] = "24"
        });

        meter.Layout.X.Should().Be(5);
        meter.Layout.Y.Should().Be(7);
        meter.Layout.Width.Should().Be(120);
        meter.Layout.Height.Should().Be(24);
    }

    [Fact]
    public async Task StringMeter_Render_DrawsText()
    {
        var measure = new StubMeasure("Time", MeasureValue.FromString("09:15:30"));
        var meter = new StringMeter("Display");
        meter.Configure(new Dictionary<string, string> { ["MeasureName"] = "Time" });
        await meter.UpdateAsync(Ctx(measure));

        var r = new RecordingCtx();
        meter.Render(r);

        r.TextCount.Should().Be(1);
        r.LastText.Should().Be("09:15:30");
    }

    [Fact]
    public async Task ProgressMeter_BoundNumber_ComputesDisplayAndBar()
    {
        var measure = new StubMeasure("Cpu", MeasureValue.FromNumber(66));
        var meter = new ProgressMeter("Load");
        meter.Configure(new Dictionary<string, string>
        {
            ["MeasureName"] = "Cpu",
            ["X"] = "0", ["Y"] = "0", ["W"] = "200", ["H"] = "10"
        });

        await meter.UpdateAsync(Ctx(measure));

        meter.DisplayValue.Should().Be(66);
        meter.BoundMeasureNames.Should().Contain("Cpu");
        meter.Render(new RecordingCtx());
    }
}
