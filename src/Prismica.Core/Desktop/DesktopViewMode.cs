namespace Prismica.Core.Desktop;

/// <summary>
/// Desktop 双视图模式（对应早期架构的「桌面模式 / 布局模式」）：
/// - <see cref="Desktop"/>：呈现模式，组件按实例渲染，窗口穿透鼠标，纯展示。
/// - <see cref="Layout"/>：布局模式，从组件库放置/调整组件，禁用穿透以便选中与编辑。
/// </summary>
public enum DesktopViewMode
{
    /// <summary>呈现模式：实时运行、鼠标穿透、纯展示。</summary>
    Desktop,

    /// <summary>布局模式：摆放/调整组件、禁用穿透、可打开属性面板编辑变量与尺寸。</summary>
    Layout
}

/// <summary>
/// 视图模式相关的纯逻辑规则（可单测，无 UI 依赖）。
/// </summary>
public static class DesktopViewModeRules
{
    /// <summary>在两个模式间切换。</summary>
    public static DesktopViewMode Toggle(DesktopViewMode mode) =>
        mode == DesktopViewMode.Desktop ? DesktopViewMode.Layout : DesktopViewMode.Desktop;

    /// <summary>
    /// 给定当前模式与「配置里是否开启点击穿透」，返回该模式下窗口是否应穿透鼠标。
    /// 呈现模式沿用配置；布局模式一律不穿透（需接收点击以选中/编辑）。
    /// </summary>
    public static bool ShouldClickThrough(DesktopViewMode mode, bool configuredClickThrough) =>
        mode == DesktopViewMode.Desktop && configuredClickThrough;

    /// <summary>人类可读标签（用于托盘通知）。</summary>
    public static string ToLabel(DesktopViewMode mode) =>
        mode == DesktopViewMode.Layout ? "Layout Mode" : "Desktop Mode";

    /// <summary>从配置字符串解析（忽略大小写；未知值回退 Desktop）。</summary>
    public static DesktopViewMode Parse(string? value) =>
        string.Equals(value, "Layout", StringComparison.OrdinalIgnoreCase)
            ? DesktopViewMode.Layout
            : DesktopViewMode.Desktop;
}
