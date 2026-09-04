using System.IO;

namespace Prismica.Core.CrashReporting;

/// <summary>
/// 把崩溃报告以 JSON 文件形式写入本地 <c>crashes/</c> 目录（默认 %LOCALAPPDATA%\Prismica\crashes）。
/// 纯 IO，不依赖 WPF；写入失败静默返回 null，绝不抛异常影响主程序。
/// </summary>
public sealed class LocalCrashSink
{
    private readonly string _directory;

    /// <summary>
    /// 构造本地落盘 sink。
    /// </summary>
    /// <param name="directory">目标目录；为空则用默认 <c>%LOCALAPPDATA%\Prismica\crashes</c>。</param>
    public LocalCrashSink(string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Prismica", "crashes")
            : directory!;
    }

    /// <summary>
    /// 写出一份崩溃报告 JSON。文件名带 UTC 时间戳 + 同毫秒自增序号以避免覆盖。
    /// 返回写出的完整路径；任何失败返回 null。
    /// </summary>
    public string? WriteReport(CrashReport report)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            var path = Path.Combine(_directory, $"crash_{stamp}.json");
            var seq = 1;
            while (File.Exists(path))
                path = Path.Combine(_directory, $"crash_{stamp}_{seq++}.json");
            File.WriteAllText(path, report.ToJson());
            return path;
        }
        catch
        {
            return null;
        }
    }
}
