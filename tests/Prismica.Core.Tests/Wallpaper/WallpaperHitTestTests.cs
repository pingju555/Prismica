using Prismica.Core.Primitives;
using Prismica.Core.Wallpaper;
using Xunit;

namespace Prismica.Core.Tests.Wallpaper;

public class WallpaperHitTestTests
{
    private static readonly IReadOnlyList<Rect> SampleContent = new[]
    {
        new Rect(100, 100, 200, 80),   // 一个 meter 内容框
        new Rect(500, 600, 120, 120),  // 另一个 meter 内容框
    };

    [Fact]
    public void IsTransparent_EmptyRects_AlwaysTransparent()
    {
        Assert.True(WallpaperHitTest.IsTransparent(new Point(10, 10), new List<Rect>()));
        Assert.True(WallpaperHitTest.IsTransparent(new Point(999, 999), new List<Rect>()));
    }

    [Fact]
    public void IsTransparent_NullRects_AlwaysTransparent()
    {
        Assert.True(WallpaperHitTest.IsTransparent(new Point(0, 0), null!));
    }

    [Fact]
    public void IsTransparent_PointInsideContent_ReturnsFalse()
    {
        // (150,140) 落在第一个内容框 (100,100,200x80) 内 -> 不透明/应接收点击
        Assert.False(WallpaperHitTest.IsTransparent(new Point(150, 140), SampleContent));
    }

    [Fact]
    public void IsTransparent_PointOutsideContent_ReturnsTrue()
    {
        // (10,10) 不在任何内容框内 -> 透明区/应穿透
        Assert.True(WallpaperHitTest.IsTransparent(new Point(10, 10), SampleContent));
        // 两框之间
        Assert.True(WallpaperHitTest.IsTransparent(new Point(400, 400), SampleContent));
    }

    [Fact]
    public void IsTransparent_BoundaryInclusive()
    {
        // 落在边界 (100,100) 应视为命中内容（矩形通常包含左/上边界）
        Assert.False(WallpaperHitTest.IsTransparent(new Point(100, 100), SampleContent));
    }
}
