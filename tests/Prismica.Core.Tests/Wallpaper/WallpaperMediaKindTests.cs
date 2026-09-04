using Prismica.Core.Wallpaper;
using Xunit;

namespace Prismica.Core.Tests.Wallpaper;

public sealed class WallpaperMediaKindTests
{
    [Theory]
    [InlineData("bg.png", WallpaperMediaKind.Png)]
    [InlineData("bg.PNG", WallpaperMediaKind.Png)]
    [InlineData("anim.gif", WallpaperMediaKind.Gif)]
    [InlineData("anim.GIF", WallpaperMediaKind.Gif)]
    [InlineData("vid.mp4", WallpaperMediaKind.Video)]
    [InlineData("vid.webm", WallpaperMediaKind.Video)]
    [InlineData("vid.avi", WallpaperMediaKind.Video)]
    [InlineData("vid.mov", WallpaperMediaKind.Video)]
    [InlineData("vid.mkv", WallpaperMediaKind.Video)]
    [InlineData("unknown.xyz", WallpaperMediaKind.Png)] // 未知扩展名按 PNG 处理（向后兼容）
    [InlineData("", WallpaperMediaKind.Png)]
    public void FromPath_MapsExtension(string path, WallpaperMediaKind expected)
    {
        Assert.Equal(expected, WallpaperMediaKindExtensions.FromPath(path));
    }
}
