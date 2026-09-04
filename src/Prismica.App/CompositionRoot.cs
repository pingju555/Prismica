using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Prismica.Core.Components;
using Prismica.Core.Formula;
using Prismica.Core.Native;
using Prismica.Core.Persistence;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Prismica.Core.Themes;
using Prismica.Core.Update;
using Prismica.App.Update;
using Prismica.Core.CrashReporting;
using Prismica.App.CrashReporting;
using Prismica.Infra.Native;
using Prismica.Infra.Embeds;
using Prismica.Infra.Wpf;

namespace Prismica.App;

/// <summary>
/// Composition root（Generic Host + DI）。
/// 注册：NativeDesktop、RenderHost、FormulaEngine、FrameScheduler、DesktopHostedService。
/// </summary>
public static class CompositionRoot
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // 基础设施
        builder.Services.AddSingleton<INativeDesktop, Win32NativeDesktop>();
        builder.Services.AddSingleton<IRenderHost, WpfRenderHost>();
        builder.Services.AddSingleton<IFormulaEngine, DefaultFormulaEngine>();
        builder.Services.AddSingleton<IFrameScheduler, DefaultFrameScheduler>();
        builder.Services.AddSingleton<ILayoutSerializer, IniLayoutSerializer>();
        builder.Services.AddSingleton(sp =>
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Components");
            return new ComponentLibrary(dir);
        });
        builder.Services.AddSingleton(sp =>
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Themes");
            return new ThemeManager(dir);
        });

        // 宿主：Desktop 覆盖窗口渲染服务（此前遗漏注册，导致 host.Run 不启动任何托管服务）
        builder.Services.AddHostedService<DesktopHostedService>();

        builder.Services.Configure<DesktopOptions>(
            builder.Configuration.GetSection("Prismica:Desktop"));

        // 自动更新：当前版本取自入口程序集 InformationalVersion（如 "0.1.0-alpha"）。
        builder.Services.AddSingleton(sp =>
        {
            var asm = Assembly.GetEntryAssembly();
            var info = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm?.GetName().Version?.ToString()
                       ?? "0.0.0";
            var plus = info.IndexOf('+');
            if (plus >= 0) info = info[..plus];
            SemVersion.TryParse(info, out var v);
            return v ?? new SemVersion(0, 0, 0);
        });

        // 更新源：配置了 UpdateUrl 走 HTTP，否则 NoOp（不检查）。
        builder.Services.AddSingleton<IUpdateSource>(sp =>
        {
            var url = sp.GetRequiredService<IOptions<DesktopOptions>>().Value.UpdateUrl;
            if (string.IsNullOrWhiteSpace(url))
                return new NoOpUpdateSource();
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            return new HttpUpdateSource(http, url!);
        });

        builder.Services.AddSingleton<UpdateChecker>();

        // 崩溃上报（#27）：本地落盘（默认）+ 可选 HTTP 上报。
        builder.Services.AddSingleton(sp =>
        {
            var asm = Assembly.GetEntryAssembly();
            var info = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm?.GetName().Version?.ToString()
                       ?? "0.0.0";
            var plus = info.IndexOf('+');
            if (plus >= 0) info = info[..plus];
            return new CrashReportContext
            {
                AppName = "Prismica",
                AppVersion = info,
            };
        });

        builder.Services.AddSingleton<LocalCrashSinkAdapter>();

        builder.Services.AddSingleton<ICrashSink[]>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<DesktopOptions>>().Value;
            var sinks = new List<ICrashSink>();
            if (opts.CrashReportEnabled)
                sinks.Add(sp.GetRequiredService<LocalCrashSinkAdapter>());
            var uploadUrl = opts.CrashReportUploadUrl;
            if (!string.IsNullOrWhiteSpace(uploadUrl))
            {
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                sinks.Add(new HttpCrashSink(uploadUrl!, http, sp.GetRequiredService<ILogger<HttpCrashSink>>()));
            }

            return sinks.ToArray();
        });

        builder.Services.AddSingleton<CrashReporter>();

        EmbedRegistry.Register("Clock", () => new ClockEmbedComponent());
        EmbedRegistry.Register("StickyNote", () => new StickyNoteEmbedComponent());
        EmbedRegistry.Register("Weather", () => new WeatherEmbedComponent());
        EmbedRegistry.Register("MusicControl", () => new MusicControlEmbedComponent());
        EmbedRegistry.Register("IconGrid", () => new IconGridEmbedComponent());

        return builder;
    }
}

/// <summary>Desktop 配置节绑定。</summary>
public sealed class DesktopOptions
{
    // 默认关闭整窗穿透（false）：覆盖窗口按"内容区域可点 + 透明空区穿透"（WM_NCHITTEST）工作。
    public bool ClickThroughEnabled { get; set; } = false;
    public bool MultiMonitor { get; set; } = true;

    /// <summary>更新清单 JSON 地址；为空则不做更新检查（使用 NoOpUpdateSource）。</summary>
    public string? UpdateUrl { get; set; }

    /// <summary>订阅渠道（如 "stable"）；为空表示不限定渠道。</summary>
    public string? UpdateChannel { get; set; }

    /// <summary>是否把预发布版视为可用更新。</summary>
    public bool UpdateIncludePrerelease { get; set; }

    /// <summary>启动时是否自动检查更新（延迟 15s 后）。</summary>
    public bool CheckUpdateOnStartup { get; set; } = true;

    // ===== 崩溃上报（#27） =====
    /// <summary>是否把崩溃写入本地 <c>crashes/</c> 目录（结构化 JSON）。默认开启。</summary>
    public bool CrashReportEnabled { get; set; } = true;

    /// <summary>
    /// 崩溃上报远端地址（application/json POST）。为空则只写本地、不上报。
    /// 例："https://diagnostics.example.com/api/crashes"
    /// </summary>
    public string? CrashReportUploadUrl { get; set; }

    // ===== 壁纸层（路线 B：插入桌面之上、透明区点击穿透） =====
    /// <summary>壁纸层配置。启用后会在虚拟桌面最底层渲染一个全屏组件作为动态壁纸。</summary>
    public WallpaperOptions Wallpaper { get; set; } = new();

    /// <summary>
    /// 启动时的视图模式：<c>Desktop</c>（呈现）或 <c>Layout</c>（布局编辑）。
    /// 运行时仍可通过托盘菜单 / Ctrl+Alt+E 切换。布局模式下禁用点击穿透以便选中与编辑实例。
    /// </summary>
    public string ViewMode { get; set; } = "Desktop";
}

/// <summary>壁纸层配置（对应 <c>Prismica:Desktop:Wallpaper</c>）。</summary>
public sealed class WallpaperOptions
{
    /// <summary>是否启用壁纸层。默认开启；找不到组件时仅记录警告并跳过。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 壁纸模式：<c>Component</c>（默认，加载 <see cref="Path"/> 指定的 .pri 组件）或
    /// <c>Image</c>（加载 <see cref="ImagePath"/> 指定的 PNG，并基于其 alpha 通道做逐像素透明穿透遮罩）。
    /// </summary>
    public string? Mode { get; set; } = "Component";

    /// <summary>
    /// 壁纸组件名（对应 Components/&lt;Name&gt;.pri）。为空则尝试默认名 <c>wallpaper</c>。
    /// 仅 Mode=Component 时使用；设为 null/空且 Components/wallpaper.pri 不存在时壁纸层不创建。
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// 图片壁纸路径（Mode=Image 时必填）。应为带 alpha 通道的 PNG；
    /// 加载时扫描其 alpha 构建遮罩，完全透明区域点击穿透、非透明区域接收点击。
    /// </summary>
    public string? ImagePath { get; set; }
}
