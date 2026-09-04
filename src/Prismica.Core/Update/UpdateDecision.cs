namespace Prismica.Core.Update;

/// <summary>升级建议：当发现可用更新时由 <see cref="UpdateDecision.Evaluate"/> 返回。</summary>
public sealed record UpdateRecommendation(
    SemVersion From,
    SemVersion To,
    bool IsMandatory,
    string? Notes,
    string? DownloadUrl,
    string? Channel);

/// <summary>
/// 升级决策：对比当前版本与远端清单，给出是否应升级的结论。
/// 纯逻辑、无 I/O，便于单测。
/// </summary>
public static class UpdateDecision
{
    /// <summary>
    /// 评估是否需要升级。返回 null 表示无需升级（已是最新、渠道不匹配或忽略了预发布）。
    /// </summary>
    /// <param name="current">当前安装版本。</param>
    /// <param name="latest">远端清单。</param>
    /// <param name="channel">当前订阅渠道（如 "stable"）；null/空表示不限定渠道。</param>
    /// <param name="includePrerelease">是否把预发布版视为可用更新。</param>
    public static UpdateRecommendation? Evaluate(
        SemVersion current,
        UpdateManifest latest,
        string? channel = null,
        bool includePrerelease = false)
    {
        if (!SemVersion.TryParse(latest.Version, out var to) || to is null)
            return null;

        // 渠道过滤：清单显式声明了渠道，且（请求渠道非空且）不匹配则跳过；清单渠道为空表示通用。
        if (!string.IsNullOrWhiteSpace(latest.Channel)
            && !string.IsNullOrWhiteSpace(channel)
            && !string.Equals(latest.Channel, channel, StringComparison.OrdinalIgnoreCase))
            return null;

        // 预发布过滤
        if (to.IsPrerelease && !includePrerelease)
            return null;

        // 远端不比当前新 → 无需更新
        if (to.CompareTo(current) <= 0)
            return null;

        bool mandatory = false;
        if (!string.IsNullOrWhiteSpace(latest.MinRequiredVersion)
            && SemVersion.TryParse(latest.MinRequiredVersion, out var min) && min is not null)
        {
            mandatory = current.CompareTo(min) < 0;
        }

        return new UpdateRecommendation(current, to, mandatory, latest.Notes, latest.DownloadUrl, latest.Channel);
    }
}
