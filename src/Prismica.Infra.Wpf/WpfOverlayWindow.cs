using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Prismica.Core.Native;
using Prismica.Core.Rendering;
using Prismica.Infra.Native;
using CorePoint = Prismica.Core.Primitives.Point;

namespace Prismica.Infra.Wpf;

/// <summary>
/// 透明置顶覆盖窗口（WPF 实现，WP3 原型切片）。
/// 施加 LAYERED/NOACTIVATE/TOPMOST 扩展样式。
/// 点击穿透为"内容区域可点"：透明空区在 WM_NCHITTEST 返回 HTTRANSPARENT 透传给下层，
/// 命中 meter 内容区域返回 HTCLIENT 接收点击；SetClickThrough(true) 可显式整窗穿透。
/// </summary>
public sealed class WpfOverlayWindow : Window, IOverlayWindow
{
    private readonly ScreenInfo _screen;
    private WpfVisualRoot? _root;
    private bool _clickThrough;
    private bool _disposed;
    private Border? _selectionBorder;

    public WpfOverlayWindow(ScreenInfo screen)
    {
        _screen = screen;
        WindowStyle = System.Windows.WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        ResizeMode = System.Windows.ResizeMode.CanResize;

        Left = screen.Bounds.X;
        Top = screen.Bounds.Y;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;

        SourceInitialized += (_, _) =>
        {
            ApplyOverlayStyles();
            HookNcHitTest();
        };

        SizeChanged += (_, _) => OnResizeEnd?.Invoke();
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
        var grid = new Grid();
        if (root is UIElement el) grid.Children.Add(el);
        _selectionBorder = new Border
        {
            BorderBrush = Brushes.Red,
            BorderThickness = new Thickness(2),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        grid.Children.Add(_selectionBorder);
        Content = grid;
    }

    /// <summary>当前渲染根（用于运行时循环与属性面板重建）。</summary>
    public IVisualRoot? Root => _root;

    /// <summary>布局模式下标记此实例被选中（显示红色描边）。</summary>
    public bool IsSelected
    {
        set => _selectionBorder!.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 显式设置点击模式。enable=true：整窗穿透（WS_EX_TRANSPARENT）；
    /// enable=false：关闭整窗穿透，改由 WM_NCHITTEST 按内容区域判定（透明空区穿透、内容可点）。
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

    private void ApplyOverlayStyles()
    {
        IntPtr hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        // 不设 WS_EX_TRANSPARENT（整窗穿透），改由 WM_NCHITTEST 做内容区域判定。
        ex |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    /// <summary>挂接 WM_NCHITTEST：透明空区 -> HTTRANSPARENT（穿透），内容区 -> HTCLIENT（可点）。</summary>
    private void HookNcHitTest()
    {
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        if (source is null) return;
        source.AddHook(WndProc);
    }

    private const double DragHandleHeight = 30;
    private const double ResizeBorder = 8;

    public event Action<string>? OnContextMenuAction;

    /// <summary>
    /// 可用组件列表，用于右键菜单的"添加组件"子菜单。
    /// </summary>
    public IReadOnlyList<AvailableComponent>? AvailableComponents { get; set; }

    /// <summary>
    /// 可用主题列表，用于右键菜单的主题切换子菜单。
    /// </summary>
    public IReadOnlyList<AvailableTheme>? AvailableThemes { get; set; }

    /// <summary>
    /// 缩放结束时触发，用于保存布局。
    /// </summary>
    public event Action? OnResizeEnd;

    /// <summary>
    /// Ctrl+Alt+E 切换"呈现/布局"视图模式。由 WndProc 在按键时触发。
    /// </summary>
    public event Action? OnToggleViewMode;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_CLOSE = 0x0010;
        const int WM_RBUTTONUP = 0x0205;

        if (msg == WM_CLOSE)
        {
            handled = true;
            System.Windows.Application.Current?.Shutdown();
            return IntPtr.Zero;
        }

        if (msg == WM_RBUTTONUP)
        {
            handled = true;
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            ShowContextMenu(x, y);
            return IntPtr.Zero;
        }

        if (msg != NativeMethods.WM_NCHITTEST)
        {
            const int WM_KEYDOWN = 0x0100;
            if (msg == WM_KEYDOWN && wParam.ToInt64() == 0x45) // 'E'
            {
                var mods = System.Windows.Input.Keyboard.Modifiers;
                if ((mods & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt)) ==
                    (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt))
                {
                    handled = true;
                    OnToggleViewMode?.Invoke();
                    return IntPtr.Zero;
                }
            }
            return IntPtr.Zero;
        }
        if (_clickThrough)
        {
            handled = true;
            return new IntPtr(NativeMethods.HTTRANSPARENT);
        }

        int screenX = (short)(lParam.ToInt64() & 0xFFFF);
        int screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        var clientPt = PointFromScreen(new Point(screenX, screenY));
        var corePt = new CorePoint(clientPt.X, clientPt.Y);

        double w = ActualWidth;
        double h = ActualHeight;
        bool left = clientPt.X < ResizeBorder;
        bool right = clientPt.X > w - ResizeBorder;
        bool top = clientPt.Y < ResizeBorder;
        bool bottom = clientPt.Y > h - ResizeBorder;

        // 角落缩放（优先级最高）
        if (top && left) { handled = true; return new IntPtr(NativeMethods.HTTOPLEFT); }
        if (top && right) { handled = true; return new IntPtr(NativeMethods.HTTOPRIGHT); }
        if (bottom && left) { handled = true; return new IntPtr(NativeMethods.HTBOTTOMLEFT); }
        if (bottom && right) { handled = true; return new IntPtr(NativeMethods.HTBOTTOMRIGHT); }

        // 边缘缩放
        if (left) { handled = true; return new IntPtr(NativeMethods.HTLEFT); }
        if (right) { handled = true; return new IntPtr(NativeMethods.HTRIGHT); }
        if (top) { handled = true; return new IntPtr(NativeMethods.HTTOP); }
        if (bottom) { handled = true; return new IntPtr(NativeMethods.HTBOTTOM); }

        // 顶部 DragHandleHeight 像素为拖拽手柄
        if (clientPt.Y < DragHandleHeight && clientPt.Y >= 0)
        {
            handled = true;
            return new IntPtr(NativeMethods.HTCAPTION);
        }

        bool onContent = _root is not null && _root.HitTestContent(corePt);
        handled = true;
        return new IntPtr(onContent ? NativeMethods.HTCLIENT : NativeMethods.HTTRANSPARENT);
    }

    private void ShowContextMenu(int x, int y)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Reload", Tag = "reload" });
        menu.Items.Add(new MenuItem { Header = "Settings", Tag = "settings" });
        menu.Items.Add(new Separator());

        // 添加组件子菜单
        if (AvailableComponents is { Count: > 0 })
        {
            var addMenu = new MenuItem { Header = "Add Component" };
            foreach (var comp in AvailableComponents)
            {
                var mi = new MenuItem
                {
                    Header = $"{comp.Name} - {comp.Description}",
                    Tag = $"add:{comp.Name}"
                };
                mi.Click += (_, _) => OnContextMenuAction?.Invoke(mi.Tag?.ToString() ?? "");
                addMenu.Items.Add(mi);
            }
            menu.Items.Add(addMenu);
        }

        // 主题切换子菜单
        if (AvailableThemes is { Count: > 0 })
        {
            var themeMenu = new MenuItem { Header = "Theme" };
            foreach (var theme in AvailableThemes)
            {
                var mi = new MenuItem
                {
                    Header = $"{theme.Name} - {theme.Description}",
                    Tag = $"theme:{theme.Name}"
                };
                mi.Click += (_, _) => OnContextMenuAction?.Invoke(mi.Tag?.ToString() ?? "");
                themeMenu.Items.Add(mi);
            }
            menu.Items.Add(themeMenu);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Remove", Tag = "remove" });

        foreach (var item in menu.Items)
        {
            if (item is MenuItem mi && mi.Tag is not null)
                mi.Click += (_, _) => OnContextMenuAction?.Invoke(mi.Tag?.ToString() ?? "");
        }

        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
        menu.PlacementRectangle = new Rect(x, y, 0, 0);
        menu.IsOpen = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _disposed = true;
    }

    public new void Show() => base.Show();
    public new void Hide() => base.Hide();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}

/// <summary>
/// 可用组件信息，用于右键菜单。
/// </summary>
public sealed class AvailableComponent
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>
/// 可用主题信息，用于右键菜单。
/// </summary>
public sealed class AvailableTheme
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
}
