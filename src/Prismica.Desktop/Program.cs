using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Prismica.App;
using Prismica.App.CrashReporting;

namespace Prismica.Desktop;

internal static class Program
{
    private const string MutexName = "Global\\PrismicaDesktop";
    private static Mutex? _mutex;

    [STAThread]
    private static void Main(string[] args)
    {
        // 单实例检查
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Probe.Line("DESKTOP-TRACE 已有实例运行，退出");
            return;
        }

        // 全局崩溃捕获：非 UI 线程 / 后台 Task 异常也落结构化报告（不重启，仅记录）。
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) ReportCrash(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportCrash(e.Exception);
            e.SetObserved();
        };

        try
        {
            RunApp(args);
        }
        catch (Exception ex)
        {
            HandleCrash(ex);
        }
        finally
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }

    private static void RunApp(string[] args)
    {
        Probe.Line("DESKTOP-TRACE Main 开始");
        var builder = CompositionRoot.CreateBuilder(args);
        Probe.Line("DESKTOP-TRACE 构建 host");
        using var host = builder.Build();
        Probe.Line("DESKTOP-TRACE host.Run");
        host.Run();
        Probe.Line("DESKTOP-TRACE host.Run 返回（服务已停止）");
    }

    /// <summary>仅上报、不重启（供全局后台异常处理器使用）。</summary>
    private static void ReportCrash(Exception ex)
    {
        var path = CrashReporter.ReportLocalFallback(ex, GetVersion());
        Probe.Line($"DESKTOP-FATAL 后台崩溃已记录: {path}");
        Probe.Line($"DESKTOP-FATAL {ex.GetType().FullName}: {ex.Message}");
    }

    /// <summary>顶层异常：上报结构化崩溃报告 + 延迟 2 秒后自重启（崩溃兜底）。</summary>
    private static void HandleCrash(Exception ex)
    {
        var path = CrashReporter.ReportLocalFallback(ex, GetVersion());
        Probe.Line($"DESKTOP-FATAL {ex.GetType().FullName}: {ex.Message}");
        Probe.Line($"DESKTOP-FATAL 栈: {ex.StackTrace}");
        Probe.Line($"DESKTOP-FATAL 崩溃日志: {path}");

        // 简单重启：延迟 2 秒后重启
        Thread.Sleep(2000);
        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exe))
        {
            Process.Start(exe);
        }
    }

    private static string GetVersion()
    {
        var asm = Assembly.GetEntryAssembly();
        var info = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info)) return info!;
        return asm?.GetName().Version?.ToString() ?? "0.0.0";
    }
}
