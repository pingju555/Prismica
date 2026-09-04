using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Prismica.Core.Native;
using Prismica.Core.Primitives;

namespace Prismica.Infra.Native;

/// <summary>
/// 桌面图标跟踪：初始枚举 + 监听桌面目录变化，产出单条 IconChangeEvent 流（符合 INativeDesktop.TrackDesktopIcons）。
/// 原型阶段基于 FileSystemWatcher 简化实现。
/// </summary>
internal sealed class DesktopIconTracker : IDisposable
{
    private readonly List<FileSystemWatcherService> _services = new();
    private readonly Action<IconChangeEvent> _onChange;
    private readonly Timer _initialTimer;
    private bool _disposed;

    public DesktopIconTracker(Action<IconChangeEvent> onChange)
    {
        _onChange = onChange;

        foreach (string path in ShellInterop.GetDesktopPaths())
        {
            var svc = new FileSystemWatcherService(path, new FileSystemWatcherOptions(true, 200), OnFileEvent);
            _services.Add(svc);
        }

        _initialTimer = new Timer(_ => EmitInitial(), null, 100, Timeout.Infinite);
    }

    private void OnFileEvent(FileChangeEvent fileEvt)
    {
        if (_disposed || string.IsNullOrEmpty(fileEvt.Path)) return;
        string name = Path.GetFileName(fileEvt.Path);
        if (name.StartsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)) return;
        bool isFolder = Directory.Exists(fileEvt.Path);
        _onChange(new IconChangeEvent(
            MapType(fileEvt.Type),
            fileEvt.Path,
            new IconData(System.Array.Empty<ArgbColor>(), 0, 0, false)));
    }

    private static IconChangeType MapType(FileChangeType t) => t switch
    {
        FileChangeType.Created => IconChangeType.Created,
        FileChangeType.Deleted => IconChangeType.Deleted,
        FileChangeType.Renamed => IconChangeType.Renamed,
        _ => IconChangeType.Updated
    };

    /// <summary>原型阶段不真正枚举图标（真实图标枚举依赖 SHChangeNotifyRegister + SysListView32），仅触发一次空信号。</summary>
    private void EmitInitial()
    {
        if (_disposed) return;
    }

    public void Dispose()
    {
        _disposed = true;
        _initialTimer?.Dispose();
        foreach (var s in _services) s.Dispose();
        _services.Clear();
    }
}
