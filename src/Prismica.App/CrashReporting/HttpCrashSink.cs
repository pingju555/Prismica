using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prismica.Core.CrashReporting;

namespace Prismica.App.CrashReporting;

/// <summary>
/// 通过 HTTP POST（application/json）把崩溃报告上报到远端诊断服务。
/// 任何失败都静默忽略（仅记日志），绝不抛异常影响主程序。
/// 仅当配置了上报地址时才注册（见 <see cref="CompositionRoot"/>）。
/// </summary>
public sealed class HttpCrashSink : ICrashSink
{
    private readonly string _uploadUrl;
    private readonly HttpClient _http;
    private readonly ILogger<HttpCrashSink> _logger;

    public HttpCrashSink(string uploadUrl, HttpClient http, ILogger<HttpCrashSink> logger)
    {
        _uploadUrl = uploadUrl;
        _http = http;
        _logger = logger;
    }

    public async Task ReportAsync(CrashReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new StringContent(report.ToJson(), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(_uploadUrl, content, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("崩溃上报 HTTP 失败：{status}", (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "崩溃上报异常（已忽略，不影响主程序）");
        }
    }
}
