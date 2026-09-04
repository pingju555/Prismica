using System.Text.Json;

namespace Prismica.Core.Update;

/// <summary>
/// 更新清单：从更新服务器拉取的 JSON 描述（字段名小驼峰，便于直接反序列化）。
/// <example>
/// {
///   "version": "1.2.0",
///   "channel": "stable",
///   "notes": "修复若干崩溃",
///   "downloadUrl": "https://example.com/Prismica-1.2.0.msi",
///   "minRequiredVersion": "1.0.0",
///   "publishedAt": "2026-09-01T08:00:00Z"
/// }
/// </example>
/// </summary>
public sealed record UpdateManifest(
    string Version,
    string? Channel,
    string? Notes,
    string? DownloadUrl,
    string? MinRequiredVersion,
    DateTimeOffset? PublishedAt)
{
    /// <summary>从 JSON 解析；空输入或非法 JSON 或缺少 version 时返回 null。</summary>
    public static UpdateManifest? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var version = ReadString(root, "version");
            if (string.IsNullOrWhiteSpace(version)) return null;

            var published = ReadString(root, "publishedAt");
            DateTimeOffset? publishedAt = null;
            if (!string.IsNullOrWhiteSpace(published)
                && DateTimeOffset.TryParse(published, out var dt))
                publishedAt = dt;

            return new UpdateManifest(
                version!,
                ReadString(root, "channel"),
                ReadString(root, "notes"),
                ReadString(root, "downloadUrl"),
                ReadString(root, "minRequiredVersion"),
                publishedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;
}
