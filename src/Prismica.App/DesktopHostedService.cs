using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prismica.Core.Components;
using Prismica.Core.Layout;
using Prismica.Core.Formula;
using Prismica.Core.Native;
using Prismica.Core.Parameters;
using Prismica.Core.Parsing;
using Prismica.Core.Persistence;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Prismica.Core.Theming;
using Prismica.Core.Themes;
using Prismica.Core.MultiScreen;
using Prismica.Core.Desktop;
using Prismica.App.Update;
using Prismica.Infra.Native;
using Prismica.Infra.Wpf;
using Application = System.Windows.Application;
using Rect = Prismica.Core.Primitives.Rect;

namespace Prismica.App;

/// <summary>
/// Desktop 宿主服务：在专用 STA 线程上创建覆盖窗口并渲染示例小部件（WP4 纵向切片）。
/// G2-R1：接入 IFrameScheduler，以 1Hz 驱动 data-track 文本刷新，打通"实时调度 → 视觉根"的真跑链路。
/// </summary>
public sealed class DesktopHostedService : IHostedService, IDisposable
{
    private readonly INativeDesktop _native;
    private readonly IRenderHost _renderHost;
    private readonly IFormulaEngine _formula;
    private readonly IFrameScheduler _scheduler;
    private readonly ILayoutSerializer _layoutSerializer;
    private readonly ComponentLibrary _componentLibrary;
    private readonly ThemeManager _themeManager;
    private readonly UpdateChecker _updateChecker;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly DesktopOptions _options;
    private readonly ILogger<DesktopHostedService> _logger;
    private readonly List<WpfOverlayWindow> _windows = new();
    // 每个覆盖窗口关联的 .pri 路径、所属屏幕、运行时，便于主题切换时重渲染。
    private readonly Dictionary<WpfOverlayWindow, (string Path, ScreenInfo? Screen, ComponentRuntime? Runtime)> _windowMeta = new();

    private Thread? _uiThread;
    private Dispatcher? _dispatcher;
    private TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly RuntimeHolder _runtimeHolder = new();
    // 跨组件共享的全局变量存储（F9）：所有组件经 MeterContext.Globals 读取同一实例，实现 #gv:Name# 实时联动。
    private readonly GlobalVariableStore _globals = new();
    private IDisposable? _frameSub;
    private readonly FrameRateGovernor _governor = new(activeFps: 60, idleFps: 1, liveFps: 2, idleFramesBeforeDrop: 30);
    private bool _contentDirty = true;
    private bool _hasLiveMeters;
    private IDisposable? _watcherHandle;
    private ComponentRuntime? _runtime;
    private LayoutDocument? _currentLayout;
    private TrayIconManager? _trayIcon;
    private DispatcherTimer? _checkpointTimer;

    // 壁纸层（路线 B）：全虚拟桌面最底层动态壁纸
    private WallpaperLayerWindow? _wallpaper;
    private IVisualRoot? _wallpaperRoot;
    private ComponentRuntime? _wallpaperRuntime;

    // 双视图模式（呈现 / 布局编辑）：布局模式下禁用穿透以便选中与编辑实例。
    private DesktopViewMode _viewMode = DesktopViewMode.Desktop;
    // 覆盖窗口 -> 其对应布局实例（仅布局模式放置/编辑的实例有；用于属性面板回写）。
    private readonly Dictionary<WpfOverlayWindow, ComponentInstance> _overlayInstances = new();
    // 运行时循环维护的活动视觉根 / 运行时（支持布局模式运行时增删组件）。
    private readonly List<IVisualRoot> _liveRoots = new();
    private readonly List<ComponentRuntime> _liveRuntimes = new();

    public DesktopHostedService(
        INativeDesktop native,
        IRenderHost renderHost,
        IFormulaEngine formula,
        IFrameScheduler scheduler,
        ILayoutSerializer layoutSerializer,
        ComponentLibrary componentLibrary,
        ThemeManager themeManager,
        UpdateChecker updateChecker,
        IHostApplicationLifetime lifetime,
        IOptions<DesktopOptions> options,
        ILogger<DesktopHostedService> logger)
    {
        _native = native;
        _renderHost = renderHost;
        _formula = formula;
        _scheduler = scheduler;
        _layoutSerializer = layoutSerializer;
        _componentLibrary = componentLibrary;
        _themeManager = themeManager;
        _updateChecker = updateChecker;
        _lifetime = lifetime;
        _options = options.Value;
        _logger = logger;
        _viewMode = DesktopViewModeRules.Parse(_options.ViewMode);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Probe.Line("DESKTOP-TRACE StartAsync 被调用，启动 STA 线程");
        _uiThread = new Thread(RunUi) { IsBackground = false, Name = "PrismicaUi" };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        Probe.Line("DESKTOP-TRACE STA 线程已 Start");
        return _started.Task;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Probe.Line("LAYOUT StopAsync 被调用");
        _checkpointTimer?.Stop();
        CommitCheckpoint();
        _scheduler.Stop();
        _frameSub?.Dispose();
        _frameSub = null;
        _watcherHandle?.Dispose();
        _watcherHandle = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _dispatcher?.BeginInvoke(DispatcherPriority.Send, () => _dispatcher.InvokeShutdown());
        return Task.CompletedTask;
    }

    private void RunUi()
    {
        try
        {
            Probe.Line("DESKTOP-TRACE 进入 RunUi (STA)");
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Exit += (_, _) =>
            {
                Probe.Line("DESKTOP-TRACE Application.Exit 触发，通知 Host 停止");
                _lifetime.StopApplication();
            };
            Probe.Line("DESKTOP-TRACE Application 已创建");
            _dispatcher = Dispatcher.CurrentDispatcher;

            Probe.Line("DESKTOP-TRACE 开始枚举屏幕");
            var screens = EnumerateScreens();
            Probe.WriteScreenProbe(screens);

            // 启动时按配置初始化视图模式（呈现/布局）；运行期仍可由托盘菜单或 Ctrl+Alt+E 切换。
            _viewMode = DesktopViewModeRules.Parse(_options.ViewMode);

            _currentLayout = TryLoadLayout();
            CreateWallpaperLayer(screens);

            if (_currentLayout is not null && _currentLayout.Instances.Count > 0)
            {
                // 布局模式：按保存的实例创建覆盖窗口（经由统一 CreateOverlay，自动注入 Interface 变量 + 按视图模式设穿透）
                foreach (var inst in _currentLayout.Instances)
                {
                    if (!inst.Enabled) continue;
                    var loaded = LoadComponent(inst.ComponentName);
                    if (loaded is null) continue;
                    var (def, runtime, path) = loaded.Value;
                    var screen = screens.FirstOrDefault(s => s.Bounds.Contains(new Prismica.Core.Primitives.Point(inst.Bounds.X + inst.Bounds.Width / 2, inst.Bounds.Y + inst.Bounds.Height / 2)))
                                 ?? screens.FirstOrDefault();

                    var overlay = CreateOverlay(def, runtime, path, inst.Bounds, screen, inst);
                    if (overlay is null) continue;
                    if (app.MainWindow is null) app.MainWindow = overlay;

                    Probe.Line($"LAYOUT 实例 {inst.Id}: {inst.ComponentName} bounds=({inst.Bounds.X},{inst.Bounds.Y},{inst.Bounds.Width}x{inst.Bounds.Height})");
                    _logger.LogInformation("布局实例 {id}: {name} 已创建", inst.Id, inst.ComponentName);
                }

                // 实例全量创建后按 ZIndex 重排层级（同位置多组件靠 Internal Z-Index 区分前后）。
                ApplyZOrder();

                if (_wallpaperRuntime is not null && _wallpaperRoot is not null)
                {
                    _liveRoots.Add(_wallpaperRoot);
                    _liveRuntimes.Add(_wallpaperRuntime);
                }
                if (_liveRoots.Count > 0 && _liveRuntimes.Count > 0)
                    StartRuntimeLoop();
            }
            else
            {
                // 多屏差异化：按桌面配置为每屏分配独立组件集
                var profileText = ScreenProfileCatalog.LoadProfileText();
                var (profile, pDiags) = ScreenProfileCatalog.Parse(profileText);
                foreach (var d in pDiags)
                    if (d.Severity == DiagnosticSeverity.Error)
                        _logger.LogWarning("多屏配置错误: {msg}", d.Message);

                var resolved = ScreenProfileCatalog.Resolve(profile, screens, pDiags);
                bool anyCreated = false;

                foreach (var (screen, components) in resolved)
                {
                    if (components.Count == 0) continue;
                    int slot = 0;
                    foreach (var compName in components)
                    {
                        var loaded = LoadComponent(compName);
                        if (loaded is null) continue;
                        var (def, runtime, path) = loaded.Value;
                        double w = def.Prismica.Width > 0 ? def.Prismica.Width : 320;
                        double h = def.Prismica.Height > 0 ? def.Prismica.Height : 200;
                        var bounds = new Prismica.Core.Primitives.Rect(screen.Bounds.X + 24 * slot, screen.Bounds.Y + 24 * slot, w, h);
                        var overlay = CreateOverlay(def, runtime, path, bounds, screen, null);
                        if (overlay is null) continue;
                        if (app.MainWindow is null) app.MainWindow = overlay;
                        slot++;
                        anyCreated = true;
                        _logger.LogInformation("屏幕 {screen} 创建组件 {comp}", screen.DeviceName, compName);
                    }
                }

                if (!anyCreated)
                {
                    // 兜底：无任何配置组件时用 SampleWidget 保证至少一屏有内容
                    var def = SampleWidget.Create();
                    var runtime = ComponentRuntime.Create(def, _formula, _native, _globals);
                    _runtimeHolder.Runtime = runtime;
                    _runtime = runtime;
                    _liveRuntimes.Add(runtime);
                    foreach (var screen in screens)
                    {
                        var ctx = new RenderContext(_formula, def.Variables, screen.DpiScale, screen.Bounds.Size);
                        IVisualRoot root = _renderHost.CreateVisualRoot(def, ctx);
                        _renderHost.ArrangeLayout(root, screen.Bounds);
                        var overlay = new WpfOverlayWindow(screen)
                        {
                            Width = screen.Bounds.Width,
                            Height = screen.Bounds.Height,
                            Left = screen.Bounds.X,
                            Top = screen.Bounds.Y
                        };
                        overlay.SetRoot(root);
                        overlay.SetClickThrough(_options.ClickThroughEnabled);
                        overlay.OnContextMenuAction += action => HandleContextMenu(action, overlay);
                        overlay.OnResizeEnd += () => SaveLayout();
                        overlay.AvailableComponents = _componentLibrary.GetAvailableComponents()
                            .Select(c => new AvailableComponent { Name = c.Name, Description = c.Description })
                            .ToList();
                        overlay.AvailableThemes = _themeManager.Themes.Values
                            .Select(t => new AvailableTheme { Name = t.Name, Description = t.Description })
                            .ToList();
                        _windows.Add(overlay);
                        _liveRoots.Add(root);
                        overlay.Show();
                        _windowMeta[overlay] = (ScreenProfileCatalog.ProfileSavePath, screen, runtime);
                        if (app.MainWindow is null) app.MainWindow = overlay;
                        Probe.WriteOverlayProbe(overlay, screen, _options.ClickThroughEnabled);
                    }
                }

                // 纳入壁纸层运行时（若存在），使其随帧调度一起刷新/动画。
                if (_wallpaperRuntime is not null)
                {
                    _liveRuntimes.Add(_wallpaperRuntime);
                    if (_wallpaperRoot is not null) _liveRoots.Add(_wallpaperRoot);
                }

                if (_windows.Count == 0 && _wallpaper is null)
                    _logger.LogWarning("未发现任何屏幕，未创建覆盖窗口。");
                else if (_liveRuntimes.Count > 0)
                    StartRuntimeLoop();
                else
                    StartLiveClock();
            }

            StartWatchingComponents();

            // 创建托盘图标
            _trayIcon = new TrayIconManager();
            _trayIcon.OnExit += () => _dispatcher?.BeginInvoke(() => app.Shutdown());
            _trayIcon.OnOpenStudio += () => _dispatcher?.BeginInvoke(() =>
            {
                _logger.LogInformation("托盘图标：打开 Studio");
                // TODO: 打开 Studio 窗口
            });
            _trayIcon.ShowNotification("Prismica", "Desktop 已启动");

            // 右键菜单"切换主题"：用 ThemeManager 的当前主题名覆盖组件的活动主题并重渲染
            _themeManager.ThemeChanged += (_, theme) =>
                _dispatcher?.BeginInvoke(() => ReSkinWithTheme(theme.Name));

            // 启动 checkpoint 定时器
            if (_windows.Count > 0)
                StartCheckpointTimer();

            // 自动更新：托盘"检查更新"触发；启动后延迟自动检查（避免影响启动速度）
            _trayIcon.OnCheckUpdates += () => TriggerUpdateCheck();

            // 双视图模式：托盘"Toggle Layout Mode" 在 呈现 ↔ 布局 间切换
            _trayIcon.OnToggleLayoutMode += () => _dispatcher?.BeginInvoke(ToggleViewMode);
            if (_options.CheckUpdateOnStartup)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(15));
                    TriggerUpdateCheck();
                });
            }

            _started.TrySetResult();
            app.Run();
        }
        catch (Exception ex)
        {
            Probe.Line($"DESKTOP-TRACE 异常: {ex.GetType().FullName}: {ex.Message}");
            Probe.Line($"DESKTOP-TRACE 栈: {ex.StackTrace}");
            var inner = ex.InnerException;
            while (inner is not null)
            {
                Probe.Line($"DESKTOP-TRACE 内部异常: {inner.GetType().FullName}: {inner.Message}");
                inner = inner.InnerException;
            }
            _logger.LogError(ex, "UI 线程异常");
            _started.TrySetException(ex);
        }
    }

    /// <summary>G2-R1：帧调度驱动时钟 meter 的实时文本（1Hz）。使用类级 _liveRoots 的首个根。</summary>
    private void StartLiveClock()
    {
        if (_liveRoots.Count == 0 || _liveRoots[0] is not WpfVisualRoot first) return;
        string meterName = "clock";
        _scheduler.SetTargetFps(1);
        _scheduler.RegisterFrameCallback(_ =>
        {
            var text = DateTime.Now.ToString("HH:mm:ss");
            _dispatcher!.Invoke(() =>
            {
                bool ok = first.SetMeterText(meterName, text);
                if (!ok)
                {
                    _logger.LogWarning("帧调度未找到 meter {meter}，停止实时刷新", meterName);
                    _scheduler.Stop();
                }
            });
        });
        _scheduler.Start();
        _logger.LogInformation("实时调度已启动：1Hz 驱动 {meter} 文本", meterName);
    }

    private const int RunFps = 10;

    /// <summary>
    /// 主题切换：用 <paramref name="themeName"/> 作为活动主题，重新解析并渲染每个窗口的组件。
    /// 组件若声明了同名 <c>[Theme.*]</c> 段，则整套令牌随之切换（一键换肤）。
    /// </summary>
    private void ReSkinWithTheme(string themeName)
    {
        try
        {
            foreach (var kvp in _windowMeta)
            {
                var (path, screen, oldRuntime) = kvp.Value;
                if (!File.Exists(path) || screen is null) continue;

                var text = File.ReadAllText(path);
                var resolved = ThemeResolver.Resolve(text, overrideName: themeName);
                var result = new IniSkinTextParser().Parse(resolved, path);
                if (!result.Success || result.Definition is null) continue;

                oldRuntime?.Dispose();
                var runtime = ComponentRuntime.Create(result.Definition, _formula, _native, _globals);
                _windowMeta[kvp.Key] = (path, screen, runtime);
                if (ReferenceEquals(kvp.Key, _windows[0])) { _runtime = runtime; _runtimeHolder.Runtime = runtime; }

                var ctx = new RenderContext(_formula, result.Definition.Variables, screen.DpiScale, screen.Bounds.Size);
                var newRoot = new WpfVisualRoot(result.Definition, ctx, runtime.Meters, runtime.Embeds);
                _renderHost.ArrangeLayout(newRoot, screen.Bounds);
                kvp.Key.SetRoot(newRoot);
            }
            _contentDirty = true;
            _logger.LogInformation("主题已切换为 {theme}，组件重渲染完成", themeName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "主题切换重渲染失败");
        }
    }

    /// <summary>
    /// 触发一次更新检查（异步）。发现可用更新时在 UI 线程弹托盘通知；检查失败静默忽略。
    /// 仅通知、不下载/安装，避免覆盖正在运行的程序。
    /// </summary>
    private void TriggerUpdateCheck()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var rec = await _updateChecker.CheckAsync();
                if (rec is null) return;
                _dispatcher?.BeginInvoke(() => _trayIcon?.ShowNotification(
                    "Prismica 更新可用",
                    $"发现新版本 {rec.To}（当前 {rec.From}）{(rec.IsMandatory ? " [必须更新]" : "")}\n{rec.Notes}",
                    ToolTipIcon.Info));
            }
            catch
            {
                // 检查失败不应影响主程序
            }
        });
    }

    /// <summary>G3-R1：帧调度驱动运行时更新与全部根失效重绘。使用类级 _liveRoots/_liveRuntimes，支持运行时增删组件。</summary>
    private void StartRuntimeLoop()
    {
        var wpfRoots = _liveRoots.OfType<WpfVisualRoot>().ToList();
        if (wpfRoots.Count == 0)
        {
            _logger.LogWarning("运行时未找到任何 WPF 视觉根，跳过实时循环。");
            return;
        }

        // 含实时 meter（时钟/CPU 等）的组件即便空闲也需低频刷新；纯静态组件可降到看门狗帧率。
        _hasLiveMeters = _liveRuntimes.Any(RuntimeHasLiveMeters);
        _contentDirty = true;
        _governor.Reset();

        var delta = TimeSpan.FromMilliseconds(1000.0 / RunFps);
        _scheduler.SetTargetFps(_governor.Update(new RenderActivity { HasActiveAnimations = false, IsDirty = true, HasLiveMeters = _hasLiveMeters }).Fps);
        _frameSub?.Dispose();
        _frameSub = _scheduler.RegisterFrameCallback(_ =>
        {
            try
            {
                var activity = new RenderActivity
                {
                    HasActiveAnimations = _scheduler.ActiveAnimationCount > 0,
                    IsDirty = _contentDirty,
                    HasLiveMeters = _hasLiveMeters
                };
                var (fps, changed) = _governor.Update(activity);
                if (changed) _scheduler.SetTargetFps(fps);

                // 空闲且无非实时内容：跳过 UpdateAsync + InvalidateVisual，避免每帧无谓的 CPU 与分配。
                bool needsRender = _contentDirty || activity.HasActiveAnimations || activity.HasLiveMeters;
                if (!needsRender) return;

                foreach (var rt in _liveRuntimes)
                {
                    rt.UpdateAsync(delta).GetAwaiter().GetResult();
                }
                _dispatcher!.BeginInvoke(() =>
                {
                    foreach (var r in _liveRoots.OfType<WpfVisualRoot>()) r.InvalidateVisual();
                });
                _contentDirty = false;
            }
            catch (Exception ex)
            {
                Probe.Line($"DESKTOP-TRACE 更新异常: {ex.GetType().Name}: {ex.Message}");
                _scheduler.Stop();
            }
        });
        _scheduler.Start();
        _logger.LogInformation("运行时调度已启动（自适应帧率）：{n} 个 runtime 根，含实时 meter={live}", wpfRoots.Count, _hasLiveMeters);
    }

    /// <summary>
    /// 启发式判断组件是否含有随时间刷新的 meter（时钟/CPU/天气/网络/内存/电量等）。
    /// 用于帧率自适应：此类组件在空闲期仍需低频刷新，而非完全静止。
    /// </summary>
    private static bool RuntimeHasLiveMeters(ComponentRuntime rt)
    {
        foreach (var key in rt.Measures.Keys)
        {
            var k = key.ToLowerInvariant();
            if (k.Contains("clock") || k.Contains("cpu") || k.Contains("time") || k.Contains("date")
                || k.Contains("net") || k.Contains("mem") || k.Contains("weather") || k.Contains("battery"))
                return true;
        }
        return false;
    }

    private void HandleContextMenu(string action, WpfOverlayWindow overlay)
    {
        if (action.StartsWith("add:"))
        {
            var componentName = action[4..];
            var component = _componentLibrary.FindComponent(componentName);
            if (component is not null)
            {
                _logger.LogInformation("右键菜单：添加组件 {name}", componentName);
                AddComponentToLayout(component, overlay);
            }
            return;
        }

        if (action.StartsWith("theme:"))
        {
            var themeName = action[6..];
            _themeManager.SwitchTheme(themeName);
            _logger.LogInformation("右键菜单：切换主题 {theme}", themeName);
            return;
        }

        switch (action)
        {
            case "reload":
                _logger.LogInformation("右键菜单：重新加载");
                if (_windowMeta.TryGetValue(overlay, out var meta) && meta.Path is not null)
                    TryReloadComponent(meta.Path);
                break;
            case "settings":
                _logger.LogInformation("右键菜单：设置（属性面板）");
                ShowPropertyWindow(overlay);
                break;
            case "remove":
                _logger.LogInformation("右键菜单：移除组件");
                RemoveComponentLive(overlay);
                break;
        }
    }

    private void AddComponentToLayout(ComponentInfo component, WpfOverlayWindow targetOverlay)
    {
        // 在目标窗口的位置偏置处添加新组件（支持同组件多实例）。
        var b = targetOverlay.Bounds;
        var newBounds = new Prismica.Core.Primitives.Rect(b.X + 50, b.Y + 50, component.DefaultWidth, component.DefaultHeight);

        var loaded = LoadComponent(component.Name);
        if (loaded is null) return;
        var (def, runtime, path) = loaded.Value;

        var newInst = new ComponentInstance(
            Id: "inst-" + Guid.NewGuid().ToString("N")[..8],
            ComponentName: component.Name,
            Bounds: newBounds,
            ZIndex: 0,
            ParameterOverrides: new Dictionary<string, object>(),
            Enabled: true
        );

        _currentLayout = _currentLayout with
        {
            Instances = (_currentLayout?.Instances ?? new List<ComponentInstance>()).Append(newInst).ToList()
        };

        // 实时创建覆盖窗口并渲染（布局模式即时可见、可编辑）。
        var screen = EnumerateScreens().FirstOrDefault(s => s.Bounds.Contains(new Prismica.Core.Primitives.Point(newBounds.X + newBounds.Width / 2, newBounds.Y + newBounds.Height / 2)))
                     ?? EnumerateScreens().FirstOrDefault();
        var overlay = CreateOverlay(def, runtime, path, newBounds, screen, newInst);
        if (overlay is not null) overlay.IsSelected = true;
        SaveLayout();
        ApplyZOrder(); // 新组件加入后重排层级
        _logger.LogInformation("已添加组件 {name} 到布局并即时渲染", component.Name);
    }

    /// <summary>
    /// 双视图模式切换（呈现 ↔ 布局）。布局模式下禁用穿透以便选中与打开属性面板；
    /// 呈现模式恢复配置所定的穿透行为。复用 lumen/Rainmeter 惯例。
    /// </summary>
    private void ToggleViewMode()
    {
        _viewMode = DesktopViewModeRules.Toggle(_viewMode);
        foreach (var w in _windows)
        {
            w.SetClickThrough(DesktopViewModeRules.ShouldClickThrough(_viewMode, _options.ClickThroughEnabled));
        }
        _logger.LogInformation("视图模式已切换为: {mode}", DesktopViewModeRules.ToLabel(_viewMode));
        Probe.Line($"VIEWMODE switch -> {_viewMode}");
    }

    /// <summary>
    /// 打开布局模式下的属性面板：依据实例绑定的 .pri 取组件定义（含 [Interface] 封装接口），
    /// 由 ComponentPropertyWindow 数据驱动生成尺寸 + 变量控件；应用后即时重渲染并回写 layout。
    /// </summary>
    private void ShowPropertyWindow(WpfOverlayWindow overlay)
    {
        if (!_overlayInstances.TryGetValue(overlay, out var inst)) return;
        if (!_windowMeta.TryGetValue(overlay, out var meta) || !File.Exists(meta.Path)) return;
        var result = new IniSkinTextParser().Parse(ThemeResolver.Resolve(File.ReadAllText(meta.Path)), meta.Path);
        if (!result.Success || result.Definition is null) return;
        var def = result.Definition;

        var dlg = new ComponentPropertyWindow(def, inst, updated =>
        {
            UpdateLayoutDocumentInstance(updated);
            _overlayInstances[overlay] = updated;
            ReloadInstanceOverlay(overlay, updated);
            SaveLayout();
        });
        dlg.Show();
    }

    /// <summary>布局模式右键"移除"：从布局与实时窗口同时删除该实例。</summary>
    private void RemoveComponentLive(WpfOverlayWindow overlay)
    {
        if (_overlayInstances.TryGetValue(overlay, out var inst))
        {
            var list = (_currentLayout?.Instances ?? new List<ComponentInstance>()).ToList();
            list.RemoveAll(i => i.Id == inst.Id);
            _currentLayout = _currentLayout with { Instances = list };
        }
        _overlayInstances.Remove(overlay);
        var oldRoot = overlay.Root;
        if (oldRoot is not null) _liveRoots.Remove(oldRoot);
        _windows.Remove(overlay);
        _windowMeta.Remove(overlay);
        overlay.Dispose();
        SaveLayout();
        ApplyZOrder(); // 移除后其余窗口层级重排
        _logger.LogInformation("已从布局移除组件实例并即时销毁窗口");
    }

    /// <summary>
    /// 以新实例覆盖（尺寸/变量）重建该实例的覆盖窗口：销毁旧窗口并新建，
    /// 经由 InterfaceBinder 重新注入变量，实现"布局模式改变量即时生效"。
    /// </summary>
    private void ReloadInstanceOverlay(WpfOverlayWindow oldOverlay, ComponentInstance inst)
    {
        if (!_windowMeta.TryGetValue(oldOverlay, out var meta)) return;
        var path = meta.Path;
        var screen = meta.Screen ?? EnumerateScreens().FirstOrDefault();
        if (screen is null) return;
        var result = new IniSkinTextParser().Parse(ThemeResolver.Resolve(File.ReadAllText(path)), path);
        if (!result.Success || result.Definition is null) return;
        var runtime = ComponentRuntime.Create(result.Definition, _formula, _native, _globals);

        var oldRoot = oldOverlay.Root;
        _windows.Remove(oldOverlay);
        _overlayInstances.Remove(oldOverlay);
        if (oldRoot is not null) _liveRoots.Remove(oldRoot);
        _windowMeta.Remove(oldOverlay);
        oldOverlay.Dispose();

        var overlay = CreateOverlay(result.Definition, runtime, path, inst.Bounds, screen, inst);
        if (overlay is not null) overlay.IsSelected = true;
    }

    /// <summary>在布局文档中插入或更新某个实例（按 Id 匹配）。</summary>
    private void UpdateLayoutDocumentInstance(ComponentInstance updated)
    {
        if (_currentLayout is null) return;
        var list = _currentLayout.Instances.ToList();
        var idx = list.FindIndex(i => i.Id == updated.Id);
        if (idx >= 0) list[idx] = updated; else list.Add(updated);
        _currentLayout = _currentLayout with { Instances = list };
    }

    private const string ComponentsDir = "Components";

    private void StartWatchingComponents()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, ComponentsDir);
            Directory.CreateDirectory(dir);
            _watcherHandle = _native.WatchFileSystem(dir,
                new FileSystemWatcherOptions(DebounceMs: 200, Recursive: false),
                OnComponentChanged);
            _logger.LogInformation("已监视组件目录: {dir}", dir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法监视组件目录，热加载不可用");
        }
    }

    private void OnComponentChanged(FileChangeEvent evt)
    {
        if (!evt.Path.EndsWith(".pri", StringComparison.OrdinalIgnoreCase)) return;
        _dispatcher?.BeginInvoke(() => TryReloadComponent(evt.Path));
    }

    private void TryReloadComponent(string priPath)
    {
        try
        {
            if (!File.Exists(priPath)) return;
            var result = new IniSkinTextParser().Parse(ThemeResolver.Resolve(File.ReadAllText(priPath)), priPath);
            if (!result.Success || result.Definition is null)
            {
                _logger.LogWarning("热加载解析失败: {path} — {diags}",
                    priPath, string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
                return;
            }

            // 只重载引用了该 .pri 的窗口（多屏差异化：每屏组件独立）
            var targets = _windows
                .Where(w => _windowMeta.TryGetValue(w, out var meta)
                            && string.Equals(meta.Path, priPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (targets.Count == 0)
            {
                _logger.LogInformation("热加载：没有窗口引用 {path}，跳过", priPath);
                return;
            }

            var runtime = ComponentRuntime.Create(result.Definition, _formula, _native, _globals);
            var screens = EnumerateScreens();
            foreach (var w in targets)
            {
                var meta = _windowMeta[w];
                var screen = meta.Screen ?? screens.FirstOrDefault();
                var bounds = new Prismica.Core.Primitives.Rect(w.Left, w.Top, w.Width, w.Height);
                var ctx = new RenderContext(_formula, result.Definition.Variables, screen?.DpiScale ?? 1.0, new Prismica.Core.Primitives.Size(bounds.Width, bounds.Height));
                var newRoot = new WpfVisualRoot(result.Definition, ctx, runtime.Meters, runtime.Embeds);
                _renderHost.ArrangeLayout(newRoot, bounds);
                meta.Runtime?.Dispose();
                _windowMeta[w] = (priPath, screen, runtime);
                if (ReferenceEquals(w, _windows[0])) { _runtime = runtime; _runtimeHolder.Runtime = runtime; }
                w.SetRoot(newRoot);
            }
            _contentDirty = true;

            _logger.LogInformation("热加载完成: {name} v{ver} (measures={m}, meters={n})",
                result.Definition.Name, result.Definition.Version,
                runtime.Measures.Count, runtime.Meters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "热加载异常: {path}", priPath);
        }
    }

    /// <summary>按组件名加载并实例化运行时；找不到 .pri 或解析失败返回 null。</summary>
    private (ComponentDefinition Def, ComponentRuntime Runtime, string Path)? LoadComponent(string componentName)
    {
        var priPath = FindPriFile(componentName);
        if (priPath is null)
        {
            _logger.LogWarning("多屏组件找不到 .pri: {name}", componentName);
            return null;
        }

        var result = new IniSkinTextParser().Parse(ThemeResolver.Resolve(File.ReadAllText(priPath)), priPath);
        if (!result.Success || result.Definition is null)
        {
            _logger.LogWarning("多屏组件解析失败: {path}", priPath);
            return null;
        }

        var runtime = ComponentRuntime.Create(result.Definition, _formula, _native, _globals);
        Probe.Line($"COMPONENT name={result.Definition.Name} version={result.Definition.Version} " +
                   $"measures={runtime.Measures.Count} meters={runtime.Meters.Count} embeds={runtime.Embeds.Count}");
        return (result.Definition, runtime, priPath);
    }

    /// <summary>
    /// 统一创建组件覆盖窗口（呈现/布局分支与右键添加组件共用）。
    /// 自动：注入 Interface 变量（#Var# 隐式绑定）；按当前视图模式设置点击穿透；
    /// 注册右键/缩放/可用组件与主题；加入活动视觉根与运行时列表（供帧循环驱动）；登记实例映射。
    /// </summary>
    private WpfOverlayWindow? CreateOverlay(
        ComponentDefinition def,
        ComponentRuntime runtime,
        string priPath,
        Prismica.Core.Primitives.Rect bounds,
        ScreenInfo? screen,
        ComponentInstance? inst)
    {
        var sc = screen ?? EnumerateScreens().FirstOrDefault();
        if (sc is null) return null;

        var overlay = new WpfOverlayWindow(sc)
        {
            Left = bounds.X,
            Top = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height
        };

        // 把实例的 Interface 覆盖经封装接口桥接进变量层（实现"布局模式改变量"需求）。
        var variables = inst is not null
            ? InterfaceBinder.ResolveVariables(def.Interface, inst.ParameterOverrides, def.Variables)
            : def.Variables;
        var ctx = new RenderContext(_formula, variables, sc.DpiScale, new Prismica.Core.Primitives.Size(bounds.Width, bounds.Height));
        IVisualRoot root = new WpfVisualRoot(def, ctx, runtime.Meters, runtime.Embeds);
        _renderHost.ArrangeLayout(root, new Prismica.Core.Primitives.Rect(0, 0, bounds.Width, bounds.Height));

        overlay.SetRoot(root);
        overlay.SetClickThrough(DesktopViewModeRules.ShouldClickThrough(_viewMode, _options.ClickThroughEnabled));
        overlay.OnContextMenuAction += action => HandleContextMenu(action, overlay);
        overlay.OnResizeEnd += () => SaveLayout();
        overlay.OnToggleViewMode += ToggleViewMode;
        overlay.AvailableComponents = _componentLibrary.GetAvailableComponents()
            .Select(c => new AvailableComponent { Name = c.Name, Description = c.Description })
            .ToList();
        overlay.AvailableThemes = _themeManager.Themes.Values
            .Select(t => new AvailableTheme { Name = t.Name, Description = t.Description })
            .ToList();
        overlay.IsSelected = false;

        _windows.Add(overlay);
        overlay.Show();
        _windowMeta[overlay] = (priPath, sc, runtime);
        _liveRoots.Add(root);
        _liveRuntimes.Add(runtime);
        if (inst is not null) _overlayInstances[overlay] = inst;
        if (_runtime is null) { _runtime = runtime; _runtimeHolder.Runtime = runtime; }

        Probe.WriteOverlayProbe(overlay, sc, !DesktopViewModeRules.ShouldClickThrough(_viewMode, _options.ClickThroughEnabled));
        _logger.LogInformation("组件覆盖窗口已创建: {comp} ({w}x{h})", def.Name, bounds.Width, bounds.Height);
        return overlay;
    }

    /// <summary>
    /// 按各实例 ZIndex 在 TOPMOST 段内重排覆盖窗口层级：升序逐一带到 HWND_TOP，
    /// 使最高 ZIndex 的窗口最终位于最上。壁纸层非 TOPMOST，不参与。
    /// 同位置多组件即借此 Internal Z-Index 区分前后层级。
    /// </summary>
    private void ApplyZOrder()
    {
        var items = _windows
            .Select(w => (Window: (IOverlayWindow)w, ZIndex: _overlayInstances.TryGetValue(w, out var inst) ? inst.ZIndex : 0))
            .ToList();
        foreach (var window in ZOrderArranger.Order(items))
            window.SetZOrder(IntPtr.Zero); // HWND_TOP：在当前 TOPMOST 段内置顶
    }

    /// <summary>
    /// 路线 B 壁纸层：在虚拟桌面最底层渲染一个全屏组件作为动态壁纸，并插入到桌面（Progman/WorkerW）之上。
    /// 透明区点击穿透到下层桌面（由 WallpaperLayerWindow 的 WM_NCHITTEST 内容矩形判定）。
    /// 找不到组件时仅记录警告并跳过（不影响 widget 正常运行）。
    /// </summary>
    private void CreateWallpaperLayer(List<ScreenInfo> screens)
    {
        if (!_options.Wallpaper.Enabled)
        {
            Probe.Line("WALLPAPER 已禁用，跳过壁纸层");
            return;
        }

        // 计算虚拟桌面边界（所有屏幕物理边界并集，可能为负数坐标）。
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in screens)
        {
            minX = Math.Min(minX, s.Bounds.X);
            minY = Math.Min(minY, s.Bounds.Y);
            maxX = Math.Max(maxX, s.Bounds.Right);
            maxY = Math.Max(maxY, s.Bounds.Bottom);
        }
        var virtualBounds = new Rect(minX, minY, maxX - minX, maxY - minY);

        var screen = new ScreenInfo("Wallpaper", virtualBounds, virtualBounds, 1.0, screens.FirstOrDefault()?.IsPrimary ?? true);
        var wallpaper = new WallpaperLayerWindow(screen)
        {
            Left = virtualBounds.X,
            Top = virtualBounds.Y,
            Width = virtualBounds.Width,
            Height = virtualBounds.Height
        };

        bool imageMode = string.Equals(_options.Wallpaper.Mode, "Image", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(_options.Wallpaper.ImagePath);

        if (imageMode)
        {
            // 媒体壁纸：按扩展名分派（PNG 走 alpha 遮罩穿透；GIF/MP4/WebM 全屏播放且整窗点击穿透，无预计算遮罩）。
            try
            {
                wallpaper.SetMedia(_options.Wallpaper.ImagePath!, virtualBounds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "壁纸图片加载失败，跳过壁纸层: {path}", _options.Wallpaper.ImagePath);
                wallpaper.Dispose();
                return;
            }
            Probe.Line($"WALLPAPER 已创建(图片): {_options.Wallpaper.ImagePath} 覆盖虚拟桌面 ({virtualBounds.Width}x{virtualBounds.Height})");
            _logger.LogInformation("图片壁纸层已创建: {path}", _options.Wallpaper.ImagePath);
        }
        else
        {
            // 组件壁纸（默认）：加载 .pri 组件并按 meter 布局矩形判定穿透。
            string candidate = !string.IsNullOrWhiteSpace(_options.Wallpaper.Path)
                ? _options.Wallpaper.Path!
                : "wallpaper";
            var loaded = LoadComponent(candidate);
            if (loaded is null)
            {
                _logger.LogWarning("壁纸组件未找到（配置名={name}），跳过壁纸层", candidate);
                wallpaper.Dispose();
                return;
            }

            var (def, runtime, _) = loaded.Value;
            var ctx = new RenderContext(_formula, def.Variables, 1.0, new Prismica.Core.Primitives.Size(virtualBounds.Width, virtualBounds.Height));
            var root = new WpfVisualRoot(def, ctx, runtime.Meters, runtime.Embeds);
            _renderHost.ArrangeLayout(root, virtualBounds);
            wallpaper.SetRoot(root);
            _wallpaperRoot = root;
            _wallpaperRuntime = runtime;
            if (_runtime is null) { _runtime = runtime; _runtimeHolder.Runtime = runtime; }
            Probe.Line($"WALLPAPER 已创建(组件): {def.Name} 覆盖虚拟桌面 ({virtualBounds.X},{virtualBounds.Y},{virtualBounds.Width}x{virtualBounds.Height})");
            _logger.LogInformation("壁纸层已创建: {comp}", def.Name);
        }

        wallpaper.SetClickThrough(false); // 默认按内容矩形/alpha 遮罩判定：透明区穿透、内容可点
        wallpaper.Show();
        // 插入桌面之上（Progman/WorkerW 之下），使图标绘制于壁纸之上、透明区点击穿透到桌面。
        NativeMethods.InsertAboveDesktop(wallpaper.Handle);

        _wallpaper = wallpaper;
    }

    private sealed class RuntimeHolder
    {
        public ComponentRuntime? Runtime { get; set; }
    }

    private List<ScreenInfo> EnumerateScreens()
    {
        if (_native is Win32NativeDesktop native)
            return new List<ScreenInfo>(native.GetScreens());

        // 回退：一个占位主屏
        return new List<ScreenInfo>() { new ScreenInfo("Primary", new Prismica.Core.Primitives.Rect(0, 0, 800, 600), new Prismica.Core.Primitives.Rect(0, 0, 800, 600), 1.0, true) };
    }

    private static string LayoutFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prismica", "layout.ini");

    private static string PendingLayoutFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prismica", "layout.pending.ini");

    private LayoutDocument? TryLoadLayout()
    {
        try
        {
            // 优先读 pending 文件（上次异常退出的 checkpoint）
            string path = File.Exists(PendingLayoutFilePath) ? PendingLayoutFilePath : LayoutFilePath;
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            var doc = _layoutSerializer.Deserialize(stream, LayoutFormat.Ini);
            if (doc.Instances.Count == 0) return null;
            Probe.Line($"LAYOUT 加载布局: {doc.Instances.Count} 个实例, source={Path.GetFileName(path)}");
            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载布局文件失败");
            return null;
        }
    }

    private void SaveLayout()
    {
        try
        {
            Probe.Line("LAYOUT SaveLayout 开始");
            var dir = Path.GetDirectoryName(LayoutFilePath)!;
            Directory.CreateDirectory(dir);

            var instances = new List<ComponentInstance>();
            for (int i = 0; i < _windows.Count; i++)
            {
                var w = _windows[i];
                var bounds = new Prismica.Core.Primitives.Rect(w.Left, w.Top, w.Width, w.Height);
                instances.Add(new ComponentInstance(
                    $"inst{i}",
                    _currentLayout?.Instances.ElementAtOrDefault(i)?.ComponentName ?? "ClockCpu",
                    bounds,
                    i,
                    _currentLayout?.Instances.ElementAtOrDefault(i)?.ParameterOverrides ?? new Dictionary<string, object>()
                ));
            }

            var doc = new LayoutDocument(
                "0.1",
                new LayoutMetadata("Prismica", "", "Auto-saved layout", DateTime.UtcNow, DateTime.UtcNow, null),
                instances);

            using var stream = File.Create(LayoutFilePath);
            _layoutSerializer.Serialize(doc, stream, LayoutFormat.Ini);
            Probe.Line($"LAYOUT 保存布局完成: {instances.Count} 个实例 -> {LayoutFilePath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存布局文件失败");
            Probe.Line($"LAYOUT 保存布局失败: {ex.Message}");
        }
    }

    /// <summary>定时 checkpoint：把当前窗口位置写入 pending 文件，异常退出后下次启动可恢复。</summary>
    private void SaveCheckpoint()
    {
        try
        {
            var dir = Path.GetDirectoryName(PendingLayoutFilePath)!;
            Directory.CreateDirectory(dir);

            var instances = new List<ComponentInstance>();
            for (int i = 0; i < _windows.Count; i++)
            {
                var w = _windows[i];
                var bounds = new Prismica.Core.Primitives.Rect(w.Left, w.Top, w.Width, w.Height);
                instances.Add(new ComponentInstance(
                    $"inst{i}",
                    _currentLayout?.Instances.ElementAtOrDefault(i)?.ComponentName ?? "ClockCpu",
                    bounds,
                    i,
                    _currentLayout?.Instances.ElementAtOrDefault(i)?.ParameterOverrides ?? new Dictionary<string, object>()
                ));
            }

            var doc = new LayoutDocument(
                "0.1",
                new LayoutMetadata("Prismica", "", "Checkpoint", DateTime.UtcNow, DateTime.UtcNow, null),
                instances);

            using var stream = File.Create(PendingLayoutFilePath);
            _layoutSerializer.Serialize(doc, stream, LayoutFormat.Ini);
        }
        catch { }
    }

    /// <summary>正常退出时：pending → layout.ini，删除 pending。</summary>
    private void CommitCheckpoint()
    {
        try
        {
            if (File.Exists(PendingLayoutFilePath))
            {
                SaveLayout();
                File.Delete(PendingLayoutFilePath);
                Probe.Line("LAYOUT 正常退出，已提交 checkpoint");
            }
        }
        catch { }
    }

    private void StartCheckpointTimer()
    {
        _checkpointTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _checkpointTimer.Tick += (_, _) => SaveCheckpoint();
        _checkpointTimer.Start();
        Probe.Line("LAYOUT checkpoint 定时器已启动 (5s)");
    }

    private string? FindPriFile(string componentName)
    {
        // 先在 Components 目录找
        var componentsDir = Path.Combine(AppContext.BaseDirectory, ComponentsDir);
        var candidate = Path.Combine(componentsDir, $"{componentName}.pri");
        if (File.Exists(candidate)) return candidate;

        // 再在 Assets\Samples 目录找
        candidate = Path.Combine(AppContext.BaseDirectory, "Assets", "Samples", $"{componentName}.pri");
        if (File.Exists(candidate)) return candidate;

        // 尝试不区分大小写
        if (Directory.Exists(componentsDir))
        {
            var match = Directory.GetFiles(componentsDir, "*.pri")
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(componentName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return null;
    }

    public void Dispose()
    {
        _checkpointTimer?.Stop();
        _trayIcon?.Dispose();
        _watcherHandle?.Dispose();
        _frameSub?.Dispose();
        _wallpaper?.Dispose();
        _wallpaperRuntime?.Dispose();
        _runtime?.Dispose();
        foreach (var w in _windows)
        {
            if (_windowMeta.TryGetValue(w, out var meta)) meta.Runtime?.Dispose();
            w.Dispose();
        }
        _windowMeta.Clear();
        _windows.Clear();
    }
}
