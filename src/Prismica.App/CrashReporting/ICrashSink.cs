using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.CrashReporting;

namespace Prismica.App.CrashReporting;

/// <summary>崩溃上报出口（sink）。本地落盘、HTTP 上报等实现此接口。</summary>
public interface ICrashSink
{
    /// <summary>上报一份崩溃报告。实现必须自行吞掉异常，绝不能影响主程序。</summary>
    Task ReportAsync(CrashReport report, CancellationToken cancellationToken = default);
}
