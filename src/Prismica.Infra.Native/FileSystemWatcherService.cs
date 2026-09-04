using System;
using System.IO;
using System.Threading;
using Prismica.Core.Native;

namespace Prismica.Infra.Native;

/// <summary>封装 FileSystemWatcher + 防抖（单事件回调，符合 INativeDesktop.WatchFileSystem 契约）。</summary>
internal sealed class FileSystemWatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _flushTimer;
    private readonly int _debounceMs;
    private readonly Action<FileChangeEvent> _onChange;
    private readonly object _gate = new();
    private FileChangeEvent? _pending;
    private bool _disposed;

    public FileSystemWatcherService(string path, FileSystemWatcherOptions opts, Action<FileChangeEvent> onChange)
    {
        _debounceMs = opts.DebounceMs;
        _onChange = onChange;
        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = opts.Recursive,
            NotifyFilter = (NotifyFilters)opts.Filters,
            EnableRaisingEvents = true
        };
        _flushTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);

        _watcher.Created += (_, e) => Enqueue(FileChangeType.Created, e.FullPath, null);
        _watcher.Deleted += (_, e) => Enqueue(FileChangeType.Deleted, e.FullPath, null);
        _watcher.Changed += (_, e) => Enqueue(FileChangeType.Changed, e.FullPath, null);
        _watcher.Renamed += (_, e) => Enqueue(FileChangeType.Renamed, e.FullPath, e.OldFullPath);
    }

    private void Enqueue(FileChangeType type, string path, string? oldPath)
    {
        if (_disposed) return;
        lock (_gate) _pending = new FileChangeEvent(type, path, oldPath);
        _flushTimer.Change(_debounceMs, Timeout.Infinite);
    }

    private void Flush()
    {
        if (_disposed) return;
        FileChangeEvent? evt;
        lock (_gate) { evt = _pending; _pending = null; }
        if (evt is not null) _onChange(evt);
    }

    public void Dispose()
    {
        _disposed = true;
        _flushTimer.Dispose();
        _watcher.Dispose();
    }
}
