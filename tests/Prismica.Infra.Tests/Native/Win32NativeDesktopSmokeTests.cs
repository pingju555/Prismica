using System;
using Prismica.Core.Native;
using Prismica.Infra.Native;
using Xunit;

namespace Prismica.Infra.Tests.Native;

/// <summary>
/// Infra.Native 冒烟测试（WP2）。
/// 仅做"可创建/可枚举/可销毁"的纵向切片验证，不追求完整 Shell/图标语义。
/// </summary>
public class Win32NativeDesktopSmokeTests
{
    [Fact]
    public void CreateOverlayWindow_ThenDispose_DoesNotThrow()
    {
        using var desktop = new Win32NativeDesktop();
        desktop.RefreshScreenLayout();

        var screens = GetScreens(desktop);
        Assert.NotEmpty(screens);

        if (screens.Count == 0) return;

        using var overlay = desktop.CreateOverlayWindow(screens[0]);
        Assert.NotEqual(IntPtr.Zero, overlay.Handle);

        overlay.Show();
        overlay.SetClickThrough(true);
        overlay.SetClickThrough(false);
        overlay.Hide();
    }

    [Fact]
    public void RefreshScreenLayout_RaisesEvent()
    {
        using var desktop = new Win32NativeDesktop();
        ScreenLayoutChanged? received = null;
        desktop.ScreenLayoutChanged += s => received = s;

        desktop.RefreshScreenLayout();

        Assert.NotNull(received);
        Assert.NotEmpty(received.Screens);
    }

    [Fact]
    public void EnumerateScreens_AllHaveValidGeometryAndDpi()
    {
        using var desktop = new Win32NativeDesktop();
        var screens = GetScreens(desktop);

        foreach (var s in screens)
        {
            // bounds 必须非空且宽高为正
            Assert.True(s.Bounds.Width > 0 && s.Bounds.Height > 0,
                $"{s.DeviceName} bounds 非法: {s.Bounds}");
            Assert.True(s.WorkingArea.Width > 0 && s.WorkingArea.Height > 0,
                $"{s.DeviceName} workingArea 非法: {s.WorkingArea}");
            // 工作区完全落在边界内
            Assert.True(s.WorkingArea.X >= s.Bounds.X && s.WorkingArea.Y >= s.Bounds.Y,
                $"{s.DeviceName} workingArea 越界(左上)");
            Assert.True(s.DpiScale > 0,
                $"{s.DeviceName} dpi 非法: {s.DpiScale}");
        }
    }

    private static System.Collections.Generic.List<ScreenInfo> GetScreens(Win32NativeDesktop desktop)
    {
        var list = new System.Collections.Generic.List<ScreenInfo>();
        desktop.ScreenLayoutChanged += s => list.AddRange(s.Screens);
        desktop.RefreshScreenLayout();
        return list;
    }
}
