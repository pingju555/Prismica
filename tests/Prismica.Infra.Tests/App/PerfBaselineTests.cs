using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Prismica.Core.Scheduling;
using Xunit;

namespace Prismica.Infra.Tests.App;

/// <summary>
/// G2-R5: 性能基线 harness。用 DefaultFrameScheduler 以 60fps 跑一小段时间，
/// 统计实际帧率、单帧分发耗时、GC 压力、托管内存增量，并把基线报表写入探针日志供离线检查。
/// 这是确认 G3 是否值得投入的技术可行性决策依据之一。
/// </summary>
public class PerfBaselineTests
{
    [Fact]
    public void FrameScheduler_Baseline_CollectsPerfStats()
    {
        // 性能基线采样默认关闭：它会以 60fps 跑 3 秒并把 PERF-BASELINE 报表写进
        // desktop-probe.log，常态化运行会污染探针日志、拖慢测试。
        // 需要时用环境变量开启：PRISMICA_RUN_PERF_BASELINE=1 dotnet test ...
        if (!string.Equals(Environment.GetEnvironmentVariable("PRISMICA_RUN_PERF_BASELINE"), "1", StringComparison.Ordinal))
            return; // 关闭状态下跳过采样：测试通过但不写 PERF-BASELINE 到探针日志、不占用 3 秒

        using var scheduler = new DefaultFrameScheduler();
        scheduler.SetTargetFps(60);

        long frameCount = 0;
        long renderWorkTicks = 0;
        var lockObj = new object();

        // 模拟每帧渲染：轻量绘制 + 短计算（模拟 data-track 更新）
        scheduler.RegisterFrameCallback(_ =>
        {
            var sw = Stopwatch.StartNew();
            double acc = 0;
            for (int i = 0; i < 200; i++) acc += Math.Sqrt(i * 1.0);
            sw.Stop();
            lock (lockObj)
            {
                frameCount++;
                renderWorkTicks += sw.ElapsedTicks;
            }
        });

        int gcBefore = GC.CollectionCount(0);
        long memBefore = GC.GetTotalMemory(false);

        scheduler.Start();
        Thread.Sleep(3000);
        scheduler.Stop();
        scheduler.Dispose();

        int gcAfter = GC.CollectionCount(0);
        long memAfter = GC.GetTotalMemory(false);

        double elapsedSec = 3.0;
        double fps = frameCount / elapsedSec;
        double avgRenderMs = (renderWorkTicks / (double)Stopwatch.Frequency) * 1000.0 / Math.Max(1, frameCount);

        var report = new StringBuilder();
        report.AppendLine($"PERF-BASELINE durationSec=3 targetFps=60 frames={frameCount} fps={fps:F1} " +
                          $"avgRenderWorkMs={avgRenderMs:F3} gcGen0Delta={gcAfter - gcBefore} " +
                          $"memDeltaBytes={memAfter - memBefore}");
        report.AppendLine($"PERF-BASELINE 判定: fps>={Math.Min(55, fps):F0}?={fps >= 30} " +
                          $"(非阻塞参考，不做硬性门禁)");

        lock (lockObj)
        {
            if (frameCount == 0)
            {
                File.AppendAllText(ProbeLog(), report.ToString(), Encoding.UTF8);
                throw new Xunit.Sdk.XunitException("调度器未产生任何帧");
            }
        }

        File.AppendAllText(ProbeLog(), report.ToString(), Encoding.UTF8);

        // 宽松断言：3 秒内至少产生若干帧且单帧工作量在合理量级（避免 flaky）
        Assert.True(frameCount >= 20, $"帧数过少: {frameCount}");
        Assert.True(avgRenderMs < 2.0, $"平均单帧渲染工作量过高: {avgRenderMs:F3}ms");
    }

    private static string ProbeLog()
    {
        string dir = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Path.GetTempPath();
        string fullDir = Path.Combine(dir, "Prismica");
        Directory.CreateDirectory(fullDir);
        return Path.Combine(fullDir, "desktop-probe.log");
    }
}
