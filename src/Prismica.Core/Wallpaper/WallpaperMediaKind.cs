using System;
using System.IO;

namespace Prismica.Core.Wallpaper;

/// <summary>壁纸媒体类型。</summary>
public enum WallpaperMediaKind
{
    /// <summary>静态 PNG（带 alpha 通道，走预计算遮罩做逐像素穿透）。</summary>
    Png,
    /// <summary>动态 GIF（逐帧动画，整窗点击穿透，无预计算遮罩）。</summary>
    Gif,
    /// <summary>视频（MP4/WebM/AVI 等，全屏循环播放，整窗点击穿透，无预计算遮罩）。</summary>
    Video
}

/// <summary>
/// 按扩展名识别壁纸媒体类型（纯逻辑，可单测）。GIF/视频不需要预计算 alpha 遮罩，
/// 运行时直接整窗点击穿透。
/// </summary>
public static class WallpaperMediaKindExtensions
{
    /// <summary>从文件路径推断媒体类型。未知扩展名按 PNG 处理（向后兼容）。</summary>
    public static WallpaperMediaKind FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return WallpaperMediaKind.Png;
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" => WallpaperMediaKind.Png,
            "gif" => WallpaperMediaKind.Gif,
            "mp4" or "webm" or "avi" or "mkv" or "mov" or "m4v" => WallpaperMediaKind.Video,
            _ => WallpaperMediaKind.Png
        };
    }
}
