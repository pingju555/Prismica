using System.Text.Json;

namespace Prismica.Core.CrashReporting;

/// <summary>
/// 结构化崩溃报告模型。纯数据，可序列化为 JSON 便于本地落盘与远程上报。
/// 不含任何 WPF / PInvoke 依赖。
/// </summary>
public sealed class CrashReport
{
    /// <summary>本报告格式的 schema 版本，便于上报端演进。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>崩溃发生 UTC 时间（ISO 8601，如 2026-09-04T05:23:11.123Z）。</summary>
    public string TimestampUtc { get; set; } = string.Empty;

    /// <summary>应用名（默认 Prismica）。</summary>
    public string AppName { get; set; } = "Prismica";

    /// <summary>应用版本（语义化版本字符串，如 0.1.0-alpha）。</summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>构建渠道（可选，如 "stable" / "beta"）。</summary>
    public string? BuildChannel { get; set; }

    /// <summary>.NET 运行时版本（Environment.Version）。</summary>
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>操作系统平台标识（如 Win32NT）。</summary>
    public string OsPlatform { get; set; } = string.Empty;

    /// <summary>操作系统版本号（Environment.OSVersion.Version）。</summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>操作系统详细描述（RuntimeInformation.OSDescription）。</summary>
    public string OsDescription { get; set; } = string.Empty;

    /// <summary>进程架构（如 X64 / Arm64）。</summary>
    public string ProcessArchitecture { get; set; } = string.Empty;

    /// <summary>当前 UI 文化（如 zh-CN）。</summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>顶层异常类型全名。</summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>顶层异常消息。</summary>
    public string ExceptionMessage { get; set; } = string.Empty;

    /// <summary>顶层异常堆栈（可能为空）。</summary>
    public string? StackTrace { get; set; }

    /// <summary>内层异常链（按从外到内顺序）。</summary>
    public List<InnerExceptionInfo> InnerExceptions { get; set; } = new();

    /// <summary>附加诊断数据（键值对，可选）。</summary>
    public Dictionary<string, string> AdditionalData { get; set; } = new();

    /// <summary>序列化为带缩进的 UTF-8 JSON，便于阅读与上报。</summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>从 JSON 反序列化（用于测试/上报端回读）。解析失败返回 null。</summary>
    public static CrashReport? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CrashReport>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>内层异常信息（用于 <see cref="CrashReport.InnerExceptions"/>）。</summary>
public sealed class InnerExceptionInfo
{
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}
