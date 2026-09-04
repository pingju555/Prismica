using System;
using System.IO;
using Prismica.Core.Primitives;

namespace Prismica.Core.Native;

public interface INativeDesktop : IDisposable
{
    IOverlayWindow CreateOverlayWindow(ScreenInfo screen);
    void SetClickThrough(IntPtr hwnd, bool enable);
    IDisposable TrackDesktopIcons(Action<IconChangeEvent> onChange);
    IDisposable WatchFileSystem(string path, FileSystemWatcherOptions opts, Action<FileChangeEvent> onChange);
    Task ExecuteVerbAsync(string filePath, string verb, IntPtr ownerHwnd);
    Task<IconData> GetIconAsync(string path, IconSize size, bool thumbnail);
    /// <summary>枚举当前桌面（用户/公共）上的文件与文件夹条目（不含 desktop.ini）。</summary>
    IReadOnlyList<DesktopIconItem> GetDesktopIcons();
    event Action<ScreenLayoutChanged> ScreenLayoutChanged;
}

/// <summary>桌面上的单个图标条目（名称 + 完整路径 + 是否文件夹）。</summary>
public sealed record DesktopIconItem(string Name, string Path, bool IsFolder);

public interface IOverlayWindow : IDisposable
{
    IntPtr Handle { get; }
    ScreenInfo Screen { get; }
    Rect Bounds { get; set; }
    void Show();
    void Hide();
    void SetClickThrough(bool enable);
    void SetZOrder(IntPtr hWndInsertAfter);
}

public sealed record ScreenInfo(string DeviceName, Rect Bounds, Rect WorkingArea, double DpiScale, bool IsPrimary);

public sealed record IconChangeEvent(IconChangeType Type, string Path, IconData? Icon);
public enum IconChangeType { Created, Deleted, Renamed, Updated, Moved }

public sealed record FileChangeEvent(FileChangeType Type, string Path, string? OldPath);
public enum FileChangeType { Created, Deleted, Changed, Renamed }

public sealed record FileChangeBatch(IReadOnlyList<FileChangeEvent> Events);

public sealed record FileSystemWatcherOptions(
    bool Recursive = true,
    int DebounceMs = 50,
    NotifyFilters Filters = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
);

public sealed record ScreenLayoutChanged(IReadOnlyList<ScreenInfo> Screens);

public sealed record IconData(ArgbColor[] Pixels, int Width, int Height, bool IsThumbnail);
public enum IconSize { Small = 16, Large = 32, ExtraLarge = 48, Jumbo = 256, Custom = -1 }