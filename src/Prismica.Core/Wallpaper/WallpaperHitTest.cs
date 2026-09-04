using System.Collections.Generic;
using Prismica.Core.Primitives;

namespace Prismica.Core.Wallpaper;

/// <summary>
/// 壁纸层透明度点击穿透的纯逻辑判定（独立于 WPF/Win32，可单测）。
/// 规则：屏幕上某点若落在任一"内容矩形"内，视为不透明/可交互（返回 false，由窗口接收点击）；
/// 否则视为透明区域，点击应穿透到下层桌面（返回 true）。
/// </summary>
public static class WallpaperHitTest
{
    /// <summary>判断 <paramref name="point"/> 是否落在透明区域（应穿透）。</summary>
    /// <param name="point">屏幕/客户区坐标下的点。</param>
    /// <param name="contentRects">壁纸中"有内容"的矩形集合（meter 布局框等）。为空表示整窗透明。</param>
    /// <returns>true = 透明、应穿透；false = 命中内容、应接收点击。</returns>
    public static bool IsTransparent(Point point, IReadOnlyList<Rect> contentRects)
    {
        if (contentRects is null || contentRects.Count == 0) return true;
        foreach (var r in contentRects)
        {
            if (r.Contains(point)) return false;
        }
        return true;
    }
}
