using Prismica.App;
using Xunit;

namespace Prismica.Infra.Tests.App;

/// <summary>
/// G2-R2: 覆盖窗口扩展样式位解码探针（纯函数单测，不依赖真实 HWND）。
/// 验证遮罩/穿透/不激活/置顶四位的位运算语义，作为人工实测日志的交叉证据。
/// </summary>
public class ProbeTests
{
    [Fact]
    public void Decode_AllFourFlagsSet_ReturnsAllTrue()
    {
        long ex = Prismica.Infra.Native.NativeMethods.WS_EX_LAYERED
                | Prismica.Infra.Native.NativeMethods.WS_EX_TRANSPARENT
                | Prismica.Infra.Native.NativeMethods.WS_EX_NOACTIVATE
                | Prismica.Infra.Native.NativeMethods.WS_EX_TOPMOST;

        var d = Probe.DecodeOverlayStyle(ex);

        Assert.True(d.Layered);
        Assert.True(d.Transparent);
        Assert.True(d.NoActivate);
        Assert.True(d.Topmost);
    }

    [Fact]
    public void Decode_NoFlags_ReturnsAllFalse()
    {
        var d = Probe.DecodeOverlayStyle(0);
        Assert.False(d.Layered);
        Assert.False(d.Transparent);
        Assert.False(d.NoActivate);
        Assert.False(d.Topmost);
    }

    [Fact]
    public void Decode_HasTopmostAndTransparent_OnlyThoseTrue()
    {
        long ex = Prismica.Infra.Native.NativeMethods.WS_EX_TOPMOST
                | Prismica.Infra.Native.NativeMethods.WS_EX_TRANSPARENT;
        var d = Probe.DecodeOverlayStyle(ex);
        Assert.True(d.Topmost);
        Assert.True(d.Transparent);
        Assert.False(d.Layered);
        Assert.False(d.NoActivate);
    }
}
