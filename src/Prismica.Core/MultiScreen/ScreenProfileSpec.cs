using System.Collections.Generic;

namespace Prismica.Core.MultiScreen;

/// <summary>
/// 单屏组件分配：匹配键 + 组件名列表。
/// 匹配键约定：<c>Primary</c> / <c>Secondary</c> / 数字索引 (0,1,..) / 设备名子串。
/// </summary>
public sealed record ScreenAssignment(
    string ScreenKey,
    IReadOnlyList<string> Components);

/// <summary>
/// 桌面多屏配置：默认组件 + 各屏分配。
/// </summary>
public sealed record DesktopProfile(
    string Version,
    IReadOnlyList<string> DefaultComponents,
    IReadOnlyList<ScreenAssignment> Screens);
