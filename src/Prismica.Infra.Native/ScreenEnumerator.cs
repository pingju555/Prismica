using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Prismica.Core.Native;
using Prismica.Core.Primitives;

namespace Prismica.Infra.Native;

/// <summary>通过 EnumDisplayMonitors 枚举当前屏幕，转换成 Core 的 ScreenInfo 集合。</summary>
internal static class ScreenEnumerator
{
    public static IReadOnlyList<ScreenInfo> GetAllScreens()
    {
        var screens = new List<ScreenInfo>();
        NativeMethods.MonitorEnumProc proc = (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
        {
            var mi = new NativeMethods.MONITORINFO();
            mi.cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>();
            if (NativeMethods.GetMonitorInfoW(hMonitor, ref mi))
            {
                bool isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                screens.Add(new ScreenInfo(
                    "Monitor" + screens.Count,
                    ToRect(mi.rcMonitor),
                    ToRect(mi.rcWork),
                    DpiHelper.GetDpiForScreen(hMonitor),
                    isPrimary
                ));
            }
            return true;
        };
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        return screens;
    }

    private static Rect ToRect(NativeMethods.RECT r)
        => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    /// <summary>虚拟桌面坐标系 = 所有屏幕物理边界的并集（可为负）。</summary>
    public static Rect GetVirtualBounds(IReadOnlyList<ScreenInfo> screens)
    {
        if (screens.Count == 0) return new Rect(0, 0, 0, 0);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in screens)
        {
            minX = Math.Min(minX, s.Bounds.X);
            minY = Math.Min(minY, s.Bounds.Y);
            maxX = Math.Max(maxX, s.Bounds.Right);
            maxY = Math.Max(maxY, s.Bounds.Bottom);
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}

internal static class DpiHelper
{
    public static double GetDpiForScreen(IntPtr hMonitor)
    {
        try
        {
            // GetDpiForMonitor 需 shcore.dll；失败回退 96
            int x = 96, y = 96;
            int hr = GetDpiForMonitor(hMonitor, 0 /*MDT_EFFECTIVE_DPI*/, ref x, ref y);
            if (hr != 0) return 1.0;
            return x / 96.0;
        }
        catch
        {
            return 1.0;
        }
    }

    [System.Runtime.InteropServices.DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, ref int dpiX, ref int dpiY);
}
