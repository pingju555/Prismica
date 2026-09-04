using System;
using System.IO;
using System.Text;
using Prismica.Core.Native;
using Prismica.Infra.Native;
using Prismica.Infra.Wpf;

namespace Prismica.App;

/// <summary>
/// G2 实测探针：把运行时状态（窗口扩展样式位、屏幕枚举、实时调度）写入日志文件，
/// 供人工跑一遍 Desktop 后离线检查实际效果。
/// </summary>
public static class Probe
{
    private static readonly object Gate = new();

    /// <summary>探针日志路径：目录下 Prismica\desktop-probe.log。</summary>
    public static string LogPath
    {
        get
        {
            string dir = Environment.GetEnvironmentVariable("LOCALAPPDATA")
                         ?? Path.GetTempPath();
            dir = Path.Combine(dir, "Prismica");
            return Path.Combine(dir, "desktop-probe.log");
        }
    }

    public static void Line(string line)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {line}{Environment.NewLine}", Encoding.UTF8);
            }
            catch { /* 探针失败不影响主流程 */ }
        }
    }

    /// <summary>读取覆盖窗口的扩展样式位并核对遮罩/穿透/不激活/置顶是否真正生效。</summary>
    public static void WriteOverlayProbe(WpfOverlayWindow window, ScreenInfo screen, bool clickThrough)
    {
        IntPtr hwnd = window.Handle;
        if (hwnd == IntPtr.Zero)
        {
            Line($"OVERLAY screen={screen.DeviceName} HANDLE=ZERO (未初始化)");
            return;
        }

        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        var d = DecodeOverlayStyle(ex);
        Line($"OVERLAY screen={screen.DeviceName} hwnd=0x{hwnd.ToInt64():X} " +
             $"LAYERED={d.Layered} TRANSPARENT={d.Transparent} NOACTIVATE={d.NoActivate} TOPMOST={d.Topmost} " +
             $"wholeWindowClickThrough={clickThrough} exStyle=0x{ex:X8}");
        // clickThrough=true => 整窗 WS_EX_TRANSPARENT 穿透; false => WM_NCHITTEST 按内容区域(hit-test)可点
        Line($"OVERLAY 判定 N1-遮罩(LAYERED+TOPMOST)={d.Layered && d.Topmost} " +
             $"N1-不夺焦(NOACTIVATE)={d.NoActivate} " +
             $"模式=contentHitTest(透明区穿透/内容可点)={!clickThrough} 整窗穿透={clickThrough}");
    }

    /// <summary>纯函数：解码扩展样式位（可单测）。</summary>
    public static OverlayStyleProbe DecodeOverlayStyle(long exStyle)
        => new(
            (exStyle & NativeMethods.WS_EX_LAYERED) != 0,
            (exStyle & NativeMethods.WS_EX_TRANSPARENT) != 0,
            (exStyle & NativeMethods.WS_EX_NOACTIVATE) != 0,
            (exStyle & NativeMethods.WS_EX_TOPMOST) != 0);

    /// <summary>覆盖窗口扩展样式探针结果。</summary>
    public sealed record OverlayStyleProbe(bool Layered, bool Transparent, bool NoActivate, bool Topmost);

    /// <summary>记录屏幕枚举结果（多屏/DPI）。</summary>
    public static void WriteScreenProbe(System.Collections.Generic.IReadOnlyList<ScreenInfo> screens)
    {
        Line($"SCREEN count={screens.Count}");
        foreach (var s in screens)
        {
            Line($"SCREEN name={s.DeviceName} primary={s.IsPrimary} dpi={s.DpiScale:F2} " +
                 $"bounds=({s.Bounds.X},{s.Bounds.Y},{s.Bounds.Width}x{s.Bounds.Height}) " +
                 $"work=({s.WorkingArea.X},{s.WorkingArea.Y},{s.WorkingArea.Width}x{s.WorkingArea.Height})");
        }
    }
}
