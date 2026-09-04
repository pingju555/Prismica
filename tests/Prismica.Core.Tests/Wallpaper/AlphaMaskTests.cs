using Prismica.Core.Wallpaper;
using Xunit;

namespace Prismica.Core.Tests.Wallpaper;

public class AlphaMaskTests
{
    // 3x3 测试图像，alpha 布局（行优先）：
    //   0   0 255
    //   0 128   0
    // 255   0   0
    // 默认 threshold=0：仅 alpha==0 视为完全透明（穿透）。
    private static readonly byte[] _pixels =
    {
        0,   0, 255,
        0, 128,   0,
        255, 0,   0
    };

    private static AlphaMask Mask(byte threshold = 0) => new(3, 3, _pixels, threshold);

    [Fact]
    public void IsTransparent_StrictThreshold_AlphaZeroPasses()
    {
        var m = Mask(0);
        Assert.True(m.IsTransparent(0, 0));  // alpha 0
        Assert.True(m.IsTransparent(1, 0));  // alpha 0
        Assert.True(m.IsTransparent(2, 1));  // alpha 0
        Assert.True(m.IsTransparent(1, 2));  // alpha 0
    }

    [Fact]
    public void IsTransparent_StrictThreshold_OpaqueBlocks()
    {
        var m = Mask(0);
        Assert.False(m.IsTransparent(2, 0)); // alpha 255
        Assert.False(m.IsTransparent(0, 2)); // alpha 255
    }

    [Fact]
    public void IsTransparent_SemiTransparent_BlockedUnderStrictThreshold()
    {
        var m = Mask(0);
        Assert.False(m.IsTransparent(1, 1)); // alpha 128 -> 非完全透明 -> 不穿透
    }

    [Fact]
    public void IsTransparent_WithTolerance_AlphaBelowThresholdPasses()
    {
        // threshold=128：alpha<=128 都视为透明（128 含在内），仅 255 不透明。
        var m = Mask(128);
        Assert.True(m.IsTransparent(1, 1));  // alpha 128 <= 128 -> 透明
        Assert.False(m.IsTransparent(2, 0)); // alpha 255 -> 不透明
    }

    [Fact]
    public void IsTransparent_OutOfBounds_IsTransparent()
    {
        var m = Mask(0);
        Assert.True(m.IsTransparent(-1, 0));
        Assert.True(m.IsTransparent(0, 3));
        Assert.True(m.IsTransparent(3, 3));
    }

    [Fact]
    public void AlphaAt_MapsRowMajor()
    {
        var m = Mask(0);
        Assert.Equal((byte)255, m.AlphaAt(2, 0));
        Assert.Equal((byte)128, m.AlphaAt(1, 1));
        Assert.Equal((byte)0, m.AlphaAt(0, 0));
    }

    [Fact]
    public void Constructor_RejectsLengthMismatch()
    {
        Assert.Throws<ArgumentException>(() => new AlphaMask(3, 3, new byte[8]));
    }
}
