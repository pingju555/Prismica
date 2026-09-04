using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Prismica.Core.Native;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Wallpaper;
using Prismica.Infra.Native;
using CorePoint = Prismica.Core.Primitives.Point;

namespace Prismica.Infra.Wpf;

/// <summary>
/// 最底层壁纸窗口（路线 B）：全虚拟桌面视口、不置顶、插入桌面（Progman/WorkerW）之上、普通窗口之下。
/// 点击穿透采用与组件覆盖窗口一致的内容矩形判定：透明空区 -> HTTRANSPARENT（穿透到下层桌面），
/// 命中壁纸内容（meter 布局区）-> HTCLIENT（接收点击）。SetClickThrough(true) 可切换为整窗穿透。
/// </summary>
public sealed class WallpaperLayerWindow : Window, IOverlayWindow
{
    private readonly ScreenInfo _screen;
    private WpfVisualRoot? _root;
    private bool _clickThrough;
    private bool _disposed;

    // 图片壁纸模式（基于预识别 alpha 遮罩的逐像素透明穿透）
    private string? _imagePath;
    private AlphaMask? _mask;
    private double _imgScaleX = 1;
    private double _imgScaleY = 1;

    public WallpaperLayerWindow(ScreenInfo screen)
    {
        _screen = screen;
        WindowStyle = System.Windows.WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = false; // 路线 B：不置顶——置于桌面之上、普通窗口之下
        ShowActivated = false;
        ShowInTaskbar = false;
        ResizeMode = System.Windows.ResizeMode.NoResize;

        Left = screen.Bounds.X;
        Top = screen.Bounds.Y;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;

        SourceInitialized += (_, _) =>
        {
            ApplyWallpaperStyles();
            HookNcHitTest();
        };
    }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    public ScreenInfo Screen => _screen;

    public Prismica.Core.Primitives.Rect Bounds
    {
        get => new(Left, Top, Width, Height);
        set { Left = value.X; Top = value.Y; Width = value.Width; Height = value.Height; }
    }

    public void SetRoot(IVisualRoot root)
    {
        _root = root as WpfVisualRoot;
        if (root is UIElement el) Content = el;
    }

    /// <summary>
    /// 显式设置点击模式。enable=true：整窗穿透（WS_EX_TRANSPARENT）；
    /// enable=false（默认）：由 WM_NCHITTEST 按内容矩形判定（透明空区穿透、内容可点）。
    /// </summary>
    public void SetClickThrough(bool enable)
    {
        _clickThrough = enable;
        IntPtr hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex = enable ? ex | NativeMethods.WS_EX_TRANSPARENT : ex & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public void SetZOrder(IntPtr hWndInsertAfter)
    {
        IntPtr hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(hwnd, hWndInsertAfter, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void ApplyWallpaperStyles()
    {
        IntPtr hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        // 设 LAYERED/NOACTIVATE；刻意不加 WS_EX_TOPMOST（路线 B）与 WS_EX_TRANSPARENT（由 WM_NCHITTEST 判透明）。
        ex |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    private void HookNcHitTest()
    {
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        if (source is null) return;
        source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_NCHITTEST) return IntPtr.Zero;

        // 整窗穿透模式：所有点击透传下层桌面。
        if (_clickThrough)
        {
            handled = true;
            return new IntPtr(NativeMethods.HTTRANSPARENT);
        }

        int screenX = (short)(lParam.ToInt64() & 0xFFFF);
        int screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        var clientPt = PointFromScreen(new System.Windows.Point(screenX, screenY));
        var corePt = new CorePoint(clientPt.X, clientPt.Y);

        bool onContent = _root is not null && _root.HitTestContent(corePt);

        // 图片壁纸模式：用预识别 alpha 遮罩做逐像素命中——
        // 完全透明像素(在本实现中 alpha<=Threshold)穿透下层桌面，非透明像素接收点击。
        if (_mask is not null)
        {
            int px = (int)(clientPt.X / _imgScaleX);
            int py = (int)(clientPt.Y / _imgScaleY);
            bool transparent = _mask.IsTransparent(px, py);
            handled = true;
            return new IntPtr(transparent ? NativeMethods.HTTRANSPARENT : NativeMethods.HTCLIENT);
        }

        handled = true;
        // 透明空区 -> 穿透；壁纸内容区 -> 接收（与组件窗口一致的"透明部分点击穿透"语义）。
        return new IntPtr(onContent ? NativeMethods.HTCLIENT : NativeMethods.HTTRANSPARENT);
    }

    /// <summary>
    /// 图片壁纸模式：加载 PNG 并以 Fill 拉伸铺满虚拟桌面视口，同时一次性扫描其 alpha 通道
    /// 构建 <see cref="AlphaMask"/>（预识别缓存）。命中测试时将客户坐标映射到图片像素查遮罩，
    /// 完全透明区域穿透、非透明区域接收点击。
    /// </summary>
    /// <param name="imagePath">带 alpha 通道的 PNG 路径。</param>
    /// <param name="virtualBounds">虚拟桌面视口（窗口尺寸与之一致，用于计算拉伸比例）。</param>
    public void SetImage(string imagePath, Prismica.Core.Primitives.Rect virtualBounds)
    {
        _imagePath = imagePath;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();

        var img = new Image
        {
            Source = bmp,
            Stretch = Stretch.Fill,
            Width = virtualBounds.Width,
            Height = virtualBounds.Height
        };
        Content = img;

        var mask = PngAlphaMask.BuildFromPng(imagePath, 0);
        _imgScaleX = virtualBounds.Width / mask.Width;
        _imgScaleY = virtualBounds.Height / mask.Height;
        _mask = mask;
        _root = null; // 图片模式下不再使用组件内容矩形判定
    }

    /// <summary>当前是否为图片壁纸模式（已加载 alpha 遮罩）。</summary>
    public bool IsImageMode => _mask is not null;

    public new void Show() => base.Show();
    public new void Hide() => base.Hide();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}
