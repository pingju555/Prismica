using System.Collections.Generic;
using Prismica.Core.Native;

namespace Prismica.Core.Layout;

/// <summary>
/// 覆盖窗口 Z 序计算（纯逻辑，可单测）。
/// 仅决定各窗口应接收 <c>SetZOrder(HWND_TOP)</c> 的先后次序，不触碰任何原生 API。
/// </summary>
public static class ZOrderArranger
{
    /// <summary>
    /// 计算应按 ZIndex 升序调用 <c>SetZOrder(HWND_TOP)</c> 的窗口序列。
    /// 升序稳定排序：ZIndex 小的先置顶，ZIndex 大的后置于顶，最终最高 ZIndex 位于最上。
    /// 相同 ZIndex 保持传入（创建）顺序。
    /// </summary>
    /// <param name="items">窗口与其 ZIndex 的配对。无实例绑定的窗口 ZIndex 视为 0。</param>
    public static IReadOnlyList<IOverlayWindow> Order(IEnumerable<(IOverlayWindow Window, int ZIndex)> items)
    {
        var list = items?.ToList() ?? new List<(IOverlayWindow Window, int ZIndex)>();
        return list
            .Select((x, i) => (x.Window, x.ZIndex, Index: i))
            .OrderBy(x => x.ZIndex)
            .ThenBy(x => x.Index)
            .Select(x => x.Window)
            .ToList();
    }
}
