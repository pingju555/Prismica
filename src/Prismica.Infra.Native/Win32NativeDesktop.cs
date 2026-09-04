using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.Native;

namespace Prismica.Infra.Native;

/// <summary>
/// INativeDesktop 的 Win32 实现（WP2 原型）。
/// 组合屏幕枚举、覆盖窗口、文件监控、桌面图标跟踪、Shell 图标/verb。
/// </summary>
public sealed class Win32NativeDesktop : INativeDesktop
{
    private bool _disposed;
    private readonly List<IDisposable> _trackers = new();

    public event Action<ScreenLayoutChanged>? ScreenLayoutChanged;

    public IOverlayWindow CreateOverlayWindow(ScreenInfo screen)
        => new Win32OverlayWindow(screen);

    public void SetClickThrough(IntPtr hwnd, bool enable)
    {
        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        if (enable) ex |= NativeMethods.WS_EX_TRANSPARENT;
        else ex &= ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public IDisposable TrackDesktopIcons(Action<IconChangeEvent> onChange)
    {
        var tracker = new DesktopIconTracker(onChange);
        _trackers.Add(tracker);
        return new DisposableAction(() => { _trackers.Remove(tracker); tracker.Dispose(); });
    }

    public IDisposable WatchFileSystem(string path, FileSystemWatcherOptions opts, Action<FileChangeEvent> onChange)
    {
        var svc = new FileSystemWatcherService(path, opts, onChange);
        _trackers.Add(svc);
        return new DisposableAction(() => { _trackers.Remove(svc); svc.Dispose(); });
    }

    public Task ExecuteVerbAsync(string filePath, string verb, IntPtr ownerHwnd)
        => ShellInterop.ExecuteVerbAsync(filePath, verb, ownerHwnd);

    public Task<IconData> GetIconAsync(string path, IconSize size, bool thumbnail)
        => ShellInterop.GetIconAsync(path, size, thumbnail);

    public IReadOnlyList<DesktopIconItem> GetDesktopIcons()
    {
        var items = new List<DesktopIconItem>();
        foreach (var dir in ShellInterop.GetDesktopPaths())
        {
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
                {
                    var name = Path.GetFileName(entry);
                    if (string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    items.Add(new DesktopIconItem(name, entry, Directory.Exists(entry)));
                }
            }
            catch
            {
                // 跳过无权限/不存在的桌面目录
            }
        }
        return items;
    }

    /// <summary>主动刷新屏幕布局并触发 ScreenLayoutChanged（原型用）。</summary>
    public void RefreshScreenLayout()
    {
        var screens = ScreenEnumerator.GetAllScreens();
        ScreenLayoutChanged?.Invoke(new ScreenLayoutChanged(screens));
    }

    /// <summary>同步枚举当前屏幕（原型便捷方法，非接口契约）。</summary>
    public System.Collections.Generic.IReadOnlyList<ScreenInfo> GetScreens()
        => ScreenEnumerator.GetAllScreens();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var t in _trackers) t.Dispose();
        _trackers.Clear();
    }

    private sealed class DisposableAction : IDisposable
    {
        private Action? _action;
        public DisposableAction(Action action) => _action = action;
        public void Dispose() { Interlocked.Exchange(ref _action, null)?.Invoke(); }
    }
}
