using System;
using System.Runtime.InteropServices;
using Prismica.Core.Native;
using Prismica.Core.Primitives;

namespace Prismica.Infra.Native;

/// <summary>
/// 透明置顶覆盖窗口（Win32 分层窗口）。
/// 采用 WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOPMOST + WS_POPUP。
/// </summary>
public sealed class Win32OverlayWindow : IOverlayWindow
{
    private const string WindowClass = "PrismicaOverlayWnd";
    private static readonly NativeMethods.WndProcDelegate WndProc = DefWndProc;
    private static bool _classRegistered;
    private static readonly object ClassLock = new();

    private readonly ScreenInfo _screen;
    private Rect _bounds;
    private bool _disposed;

    public Win32OverlayWindow(ScreenInfo screen)
    {
        _screen = screen;
        _bounds = screen.Bounds;

        EnsureClassRegistered();

        Handle = NativeMethods.CreateWindowEx(
            NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST,
            WindowClass,
            "PrismicaOverlay",
            NativeMethods.WS_POPUP,
            (int)_bounds.X, (int)_bounds.Y, (int)_bounds.Width, (int)_bounds.Height,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (Handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new NativeException(err, $"CreateWindowEx 失败 (0x{err:X8})", "CreateWindowEx");
        }
    }

    public IntPtr Handle { get; }

    public ScreenInfo Screen => _screen;

    public Rect Bounds
    {
        get => _bounds;
        set
        {
            _bounds = value;
            if (!_disposed)
                NativeMethods.MoveWindow(Handle, (int)value.X, (int)value.Y, (int)value.Width, (int)value.Height, true);
        }
    }

    public void Show() => NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOW);

    public void Hide() => NativeMethods.ShowWindow(Handle, NativeMethods.SW_HIDE);

    public void SetClickThrough(bool enable)
    {
        long ex = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        if (enable) ex |= NativeMethods.WS_EX_TRANSPARENT;
        else ex &= ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public void SetZOrder(IntPtr hWndInsertAfter)
        => NativeMethods.SetWindowPos(Handle, hWndInsertAfter, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != IntPtr.Zero) NativeMethods.DestroyWindow(Handle);
    }

    private static void EnsureClassRegistered()
    {
        lock (ClassLock)
        {
            if (_classRegistered) return;
            var wc = new NativeMethods.WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProc),
                hInstance = IntPtr.Zero,
                lpszClassName = WindowClass,
                hCursor = IntPtr.Zero
            };
            // RegisterClassExW 返回 ushort ATOM；0 表示失败（类已存在时也会失败，可忽略）
            NativeMethods.RegisterClassExW(ref wc);
            _classRegistered = true;
        }
    }

    private static IntPtr DefWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        => NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
}
