using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.CrashReporting;

namespace Prismica.App.CrashReporting;

/// <summary>
/// 把 Core 的 <see cref="LocalCrashSink"/>（纯 IO 落盘）适配为 <see cref="ICrashSink"/>，
/// 以便统一纳入 <see cref="CrashReporter"/> 的 sink 管线。
/// </summary>
public sealed class LocalCrashSinkAdapter : ICrashSink
{
    private readonly LocalCrashSink _inner;

    public LocalCrashSinkAdapter(string? directory = null)
    {
        _inner = new LocalCrashSink(directory);
    }

    public Task ReportAsync(CrashReport report, CancellationToken cancellationToken = default)
    {
        _inner.WriteReport(report);
        return Task.CompletedTask;
    }
}
