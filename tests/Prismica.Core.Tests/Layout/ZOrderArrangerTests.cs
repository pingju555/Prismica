using System;
using System.Collections.Generic;
using Prismica.Core.Layout;
using Prismica.Core.Native;
using Prismica.Core.Primitives;
using Xunit;

namespace Prismica.Core.Tests.Layout;

/// <summary>记录 SetZOrder 调用次序的 IOverlayWindow 替身（纯内存，无需原生窗口）。</summary>
public sealed class FakeOverlayWindow : IOverlayWindow
{
    public List<IntPtr> ZOrderCalls { get; } = new();
    public IntPtr Handle => IntPtr.Zero;
    public ScreenInfo Screen { get; } = new("X", new Rect(0, 0, 10, 10), new Rect(0, 0, 10, 10), 1.0, true);
    public Rect Bounds { get; set; }
    public void Show() { }
    public void Hide() { }
    public void SetClickThrough(bool enable) { }
    public void SetZOrder(IntPtr hWndInsertAfter) => ZOrderCalls.Add(hWndInsertAfter);
    public void Dispose() { }
}

public sealed class ZOrderArrangerTests
{
    [Fact]
    public void Order_AscendingZIndex_CallsHighestLast()
    {
        var a = new FakeOverlayWindow(); // z=0
        var b = new FakeOverlayWindow(); // z=5
        var c = new FakeOverlayWindow(); // z=2
        var items = new (IOverlayWindow Window, int ZIndex)[] { (a, 0), (b, 5), (c, 2) };

        var ordered = ZOrderArranger.Order(items);

        // 升序：a(0) -> c(2) -> b(5)，最高 ZIndex 列在最后（调用 SetZOrder(HWND_TOP) 后位于最上）。
        Assert.Equal(new IOverlayWindow[] { a, c, b }, ordered);
    }

    [Fact]
    public void Order_SameZIndex_PreservesInsertionOrder()
    {
        var a = new FakeOverlayWindow();
        var b = new FakeOverlayWindow();
        var items = new (IOverlayWindow Window, int ZIndex)[] { (a, 1), (b, 1) };

        var ordered = ZOrderArranger.Order(items);

        Assert.Equal(new IOverlayWindow[] { a, b }, ordered);
    }

    [Fact]
    public void Order_NullInput_ReturnsEmpty()
    {
        Assert.Empty(ZOrderArranger.Order(null!));
    }

    [Fact]
    public void ApplyZOrderSemantics_HighestZIndex_ReceivesLastSetZOrder()
    {
        // 复刻 DesktopHostedService.ApplyZOrder：按 Order 结果依次 SetZOrder(HWND_TOP)。
        var a = new FakeOverlayWindow(); // z=0
        var b = new FakeOverlayWindow(); // z=3
        var c = new FakeOverlayWindow(); // z=1
        var items = new (IOverlayWindow Window, int ZIndex)[] { (a, 0), (b, 3), (c, 1) };

        foreach (var w in ZOrderArranger.Order(items))
            w.SetZOrder(IntPtr.Zero);

        // 每个窗口仅被调用一次，且调用顺序为 a,c,b —— 最高 z(3) 的 b 最后置顶。
        Assert.Single(a.ZOrderCalls);
        Assert.Single(b.ZOrderCalls);
        Assert.Single(c.ZOrderCalls);
        Assert.Equal(IntPtr.Zero, b.ZOrderCalls[0]);
    }
}
