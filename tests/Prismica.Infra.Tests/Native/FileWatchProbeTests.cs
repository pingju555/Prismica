using System;
using System.IO;
using System.Threading;
using Prismica.Core.Native;
using Prismica.Infra.Native;
using Xunit;

namespace Prismica.Infra.Tests.Native;

/// <summary>
/// G2-R4: 热加载（file watch）探针。验证 WatchFileSystem 在文件变化后经防抖触发单事件回调。
/// </summary>
public class FileWatchProbeTests
{
    [Fact]
    public void WatchFileSystem_RaisesOnFileChange_AfterDebounce()
    {
        string dir = Path.Combine(Path.GetTempPath(), "prismica-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        using var desktop = new Win32NativeDesktop();
        using var manual = new ManualResetEventSlim();

        FileChangeEvent? received = null;
        var watch = desktop.WatchFileSystem(dir, new FileSystemWatcherOptions(DebounceMs: 150), e =>
        {
            received = e;
            manual.Set();
        });

        string file = Path.Combine(dir, "hot.pri");
        File.WriteAllText(file, "[Prismica]\nVersion=0.1\n");

        // 等待防抖后回调触发
        bool fired = manual.Wait(TimeSpan.FromSeconds(5));

        watch.Dispose();

        try { Directory.Delete(dir, true); } catch { }

        Assert.True(fired, "文件变化后未触发 FileChangeEvent");
        Assert.NotNull(received);
        Assert.Equal(FileChangeType.Changed, received!.Type);
        Assert.Equal(file, received.Path);
    }
}
