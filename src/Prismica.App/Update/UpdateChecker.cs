using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prismica.Core.Update;

namespace Prismica.App.Update;

/// <summary>
/// 更新检查器：组合当前版本、更新源与配置，执行一次检查并给出结论。
/// 仅做"检查 + 决策"，不负责下载/安装（避免静默覆盖正在运行的程序）。
/// 发现可用更新时触发 <see cref="UpdateAvailable"/> 事件，便于上层弹通知。
/// </summary>
public sealed class UpdateChecker
{
    private readonly IUpdateSource _source;
    private readonly SemVersion _current;
    private readonly DesktopOptions _options;
    private readonly ILogger<UpdateChecker> _logger;

    /// <summary>发现可用更新时触发（含结论）。</summary>
    public event Action<UpdateRecommendation>? UpdateAvailable;

    public UpdateChecker(
        IUpdateSource source,
        SemVersion current,
        IOptions<DesktopOptions> options,
        ILogger<UpdateChecker> logger)
    {
        _source = source;
        _current = current;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>当前安装版本。</summary>
    public SemVersion CurrentVersion => _current;

    /// <summary>
    /// 执行一次更新检查。任何异常都被吞掉并返回 null（检查失败不应影响程序运行）。
    /// </summary>
    public async Task<UpdateRecommendation?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var manifest = await _source.GetLatestAsync(cancellationToken);
            if (manifest is null)
            {
                _logger.LogInformation("更新检查：无可用的更新清单");
                return null;
            }

            var rec = UpdateDecision.Evaluate(_current, manifest, _options.UpdateChannel, _options.UpdateIncludePrerelease);
            if (rec is not null)
            {
                _logger.LogInformation("更新检查：发现新版本 {to}（当前 {from}，强制={mandatory}）", rec.To, rec.From, rec.IsMandatory);
                UpdateAvailable?.Invoke(rec);
            }
            else
            {
                _logger.LogInformation("更新检查：已是最新（当前 {cur}）", _current);
            }
            return rec;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新检查失败");
            return null;
        }
    }
}
