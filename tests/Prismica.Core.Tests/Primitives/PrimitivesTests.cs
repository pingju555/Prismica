using Prismica.Core.Primitives;
using Xunit;

namespace Prismica.Core.Tests.Primitives;

public class PrimitivesTests
{
    [Fact]
    public void ArgbColor_FromHex_SixDigitsIsOpaqueRed()
    {
        var c = ArgbColor.FromHex("FF0000");
        Assert.Equal(255, c.A);
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
        Assert.True(c.IsOpaque);
    }

    [Fact]
    public void ArgbColor_FromHex_EightDigitsWithAlpha()
    {
        var c = ArgbColor.FromHex("#00FF0080");
        Assert.Equal(0, c.A);
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(128, c.B);
        Assert.Equal("#00FF0080", c.ToHex());
        Assert.True(c.IsTransparent);
    }

    [Fact]
    public void ArgbColor_FromRgba_PacksChannels()
    {
        var c = ArgbColor.FromRgba(1, 2, 3, 4);
        Assert.Equal(4, c.A);
        Assert.Equal(1, c.R);
        Assert.Equal(2, c.G);
        Assert.Equal(3, c.B);
    }

    [Fact]
    public void ArgbColor_ToHex_WithoutAlpha()
    {
        Assert.Equal("#FF0000", ArgbColor.FromRgb(255, 0, 0).ToHex(includeAlpha: false));
    }

    [Fact]
    public void ArgbColor_ImplicitUintConversion()
    {
        var c = ArgbColor.FromRgb(10, 20, 30);
        uint v = c;
        Assert.Equal(c.Value, v);
        ArgbColor back = 0xAA0A141E;
        Assert.Equal((byte)0xAA, back.A);
    }

    [Fact]
    public void Rect_Intersect_Overlap()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);
        Assert.Equal(new Rect(5, 5, 5, 5), a.Intersect(b));
    }

    [Fact]
    public void Rect_Intersect_NoOverlap_IsEmpty()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(20, 20, 5, 5);
        Assert.Equal(Rect.Empty, a.Intersect(b));
    }

    [Fact]
    public void Rect_Union_SpansBoth()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(20, 0, 10, 10);
        Assert.Equal(new Rect(0, 0, 30, 10), a.Union(b));
    }

    [Fact]
    public void Rect_Contains_InsideAndOutside()
    {
        var r = new Rect(0, 0, 10, 10);
        Assert.True(r.Contains(new Point(5, 5)));
        Assert.False(r.Contains(new Point(10, 10))); // 右/下边界开区间
        Assert.True(r.Contains(new Point(0, 0)));
    }

    [Fact]
    public void Rect_InflateAndOffset()
    {
        var r = new Rect(10, 10, 20, 20);
        Assert.Equal(new Rect(5, 5, 30, 30), r.Inflate(5, 5));
        Assert.Equal(new Rect(15, 12, 20, 20), r.Offset(5, 2));
    }

    [Fact]
    public void Size_Operators_AndClamp()
    {
        Assert.Equal(new Size(4, 6), new Size(1, 2) + new Size(3, 4));
        Assert.Equal(new Size(2, 4), new Size(1, 2) * 2);
        var clamped = new Size(50, -5).Clamp(new Size(0, 0), new Size(100, 100));
        Assert.Equal(new Size(50, 0), clamped);
    }

    [Fact]
    public void Point_Operators_AndDistance()
    {
        Assert.Equal(new Point(4, 6), new Point(1, 2) + new Point(3, 4));
        Assert.Equal(5.0, new Point(0, 0).DistanceTo(new Point(3, 4)), 6);
    }

    [Fact]
    public void Thickness_InflateAndDeflate()
    {
        var t = Thickness.Uniform(2);
        Assert.Equal(new Rect(-2, -2, 14, 14), t.Inflate(new Rect(0, 0, 10, 10)));
        Assert.Equal(new Rect(2, 2, 6, 6), t.Deflate(new Rect(0, 0, 10, 10)));
        Assert.Equal(4, t.Horizontal);
    }

    [Fact]
    public void CornerRadius_IsUniform()
    {
        Assert.True(CornerRadius.Uniform(5).IsUniform);
        Assert.False(new CornerRadius(1, 2, 3, 4).IsUniform);
    }

    [Fact]
    public void Matrix3x2_TranslationTransformsPoint()
    {
        var m = Matrix3x2.Translation(10, 20);
        Assert.Equal(new Point(11, 21), m.Transform(new Point(1, 1)));
    }

    [Fact]
    public void Transform_TranslationAndMultiply()
    {
        var t = Transform.CreateTranslation(5, 5);
        Assert.Equal(new Point(6, 6), t.TransformPoint(new Point(1, 1)));
        var combined = Transform.CreateTranslation(1, 0) * Transform.CreateTranslation(2, 0);
        Assert.Equal(3, combined.Matrix.M31, 6);
    }
}
