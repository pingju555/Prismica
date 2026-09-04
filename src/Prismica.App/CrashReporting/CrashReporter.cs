using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prismica.Core.CrashReporting;

namespace Prismica.App.CrashReporting;

/// <summary>
/// 崩溃上报编排器：把异常构造成 <see cref="CrashReport"/>，依次投递到所有已注册 sink（本地落盘 / HTTP 等）。
/// 设计原则：任一 sink 失败都被吞掉，绝不影响主程序继续运行或退出流程。
/// </summary>
public sealed class CrashReporter
{
    private readonly ICrashSink[] _sinks;
    private readonly CrashReportContext _context;
    private readonly ILogger<CrashReporter> _logger;

    public CrashReporter(IEnumerable<ICrashSink> sinks, CrashReportContext context, ILogger<CrashReporter> logger)
    {
        _sinks = sinks is null ? Array.Empty<ICrashSink>() : System.Linq.Enumerable.ToArray(sinks);
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 构建并上报崩溃报告。返回构造出的报告（便于上层记录路径或日志）。
    /// </summary>
    public async Task<CrashReport> ReportAsync(Exception ex, CancellationToken cancellationToken = default)
    {
        var report = CrashReportBuilder.Build(ex, _context);
        foreach (var sink in _sinks)
        {
            try
            {
                await sink.ReportAsync(report, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception sinkEx)
            {
                _logger.LogWarning(sinkEx, "崩溃上报 sink 失败（已忽略）");
            }
        }

        return report;
    }

    /// <summary>
    /// 无 DI 时的兜底：直接构造并写入本地 JSON（用于 host 构建前 / 程序最外层崩溃）。
    /// 不抛异常、不依赖任何服务。
    /// </summary>
    /// <returns>写出的本地文件路径；失败返回 null。</returns>
    public static string? ReportLocalFallback(Exception ex, string appVersion, string? appName = null, string? directory = null)
    {
        try
        {
            var ctx = new CrashReportContext { AppVersion = appVersion };
            if (!string.IsNullOrEmpty(appName)) ctx.AppName = appName!;
            var report = CrashReportBuilder.Build(ex, ctx);
            return new LocalCrashSink(directory).WriteReport(report);
        }
        catch
        {
            return null;
        }
    }
}
