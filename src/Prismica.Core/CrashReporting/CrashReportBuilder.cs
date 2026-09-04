using System.Globalization;
using System.Runtime.InteropServices;

namespace Prismica.Core.CrashReporting;

/// <summary>
/// 构建崩溃报告所需的上下文（版本等由调用方注入，便于测试固定值）。
/// </summary>
public sealed class CrashReportContext
{
    /// <summary>应用名（默认 Prismica）。</summary>
    public string AppName { get; set; } = "Prismica";

    /// <summary>应用版本（语义化版本字符串）。必填（至少为空串）。</summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>构建渠道（可选）。</summary>
    public string? BuildChannel { get; set; }

    /// <summary>附加诊断数据（可选）。构建时会复制一份，避免调用方后续修改影响报告。</summary>
    public Dictionary<string, string>? AdditionalData { get; set; }
}

/// <summary>
/// 从异常与环境构造结构化 <see cref="CrashReport"/> 的纯逻辑（可单测）。
/// 环境读取（OS / 运行时 / 架构 / 文化）在构建时采集，版本/渠道/附加数据由 <see cref="CrashReportContext"/> 提供。
/// </summary>
public static class CrashReportBuilder
{
    public static CrashReport Build(Exception ex, CrashReportContext context)
    {
        var report = new CrashReport
        {
            TimestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            AppName = context.AppName,
            AppVersion = context.AppVersion,
            BuildChannel = context.BuildChannel,
            RuntimeVersion = Environment.Version.ToString(),
            OsPlatform = Environment.OSVersion.Platform.ToString(),
            OsVersion = Environment.OSVersion.Version.ToString(),
            OsDescription = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            Culture = CultureInfo.CurrentCulture.Name,
            ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
            ExceptionMessage = ex.Message,
            StackTrace = ex.StackTrace,
            AdditionalData = context.AdditionalData is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(context.AdditionalData),
        };

        var inner = ex.InnerException;
        while (inner is not null)
        {
            report.InnerExceptions.Add(new InnerExceptionInfo
            {
                ExceptionType = inner.GetType().FullName ?? inner.GetType().Name,
                Message = inner.Message,
                StackTrace = inner.StackTrace,
            });
            inner = inner.InnerException;
        }

        return report;
    }
}
