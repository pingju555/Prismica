# Prismica 开发计划

> 更新时间：2026-09-03
> 项目路径：`D:\Main\AI_Quest_Project\Prismica`

---

## 已完成 ✅

| # | 功能 | 说明 |
|---|------|------|
| 1 | G3 Alpha INC-F | Studio 预览 runtime 化、热加载、embed 渲染 |
| 2 | Measure 名修复 | `IniSkinTextParser` 保留完整节名（`MeasureClock` 而非 `Clock`） |
| 3 | 布局持久化 | `layout.ini` 保存/恢复窗口位置 |
| 4 | 托盘图标 | 右键菜单 (Open Studio / Auto Start / Exit) |
| 5 | 开机自启 | Startup 文件夹快捷方式 |
| 6 | 崩溃兜底 | 单实例 Mutex + 崩溃日志 + 自动重启 |
| 7 | Checkpoint 恢复 | 每 5s 写 `layout.pending.ini`，异常退出后下次启动恢复 |
| 8 | 窗口拖拽 | 顶部 30px 拖拽手柄，`WM_NCHITTEST` 返回 `HTCAPTION`，支持拖动移动 |
| 9 | 窗口缩放 | 8px 缩放边框，支持边缘/角落拖拽调整大小，缩放结束自动保存布局 |
| 10 | 右键菜单 | `WM_RBUTTONUP` → 弹出 WPF ContextMenu（Reload / Settings / Add Component / Remove） |
| 11 | 多组件叠加 | 同时加载多个 `.pri`，每个实例独立窗口/位置/大小，所有 runtime 同步更新 |
| 12 | 组件库目录 | `ComponentLibrary` 扫描 `Components/` 目录，列出可用 `.pri` 供添加 |
| 13 | 音乐控制 embed | `MusicControlEmbedComponent`，SMTC 媒体会话占位实现 |
| 15 | 天气 embed | `WeatherEmbedComponent`，调用 wttr.in API 获取当前天气 |
| 16 | 便签 embed | `StickyNoteEmbedComponent`，本地文本便签，持久化存储 |
| 17 | Studio 参数面板 | 增强 Studio 支持 `[Interface.*]` 参数可视化配置（Number/Color/Bool/Select/Text） |
| 19 | Studio 组件库 | Studio 左侧组件库面板，双击加载组件，显示名称/描述 |
| 18 | **Studio 实时预览** | 编辑 `.pri` 时预览实时刷新（编辑即预览防抖重建 + 启动即显示预览） |
| 20 | **参数 Schema 设计器** | 可视化定义 `[Interface.*]` 参数：类型/默认值/标签/范围/选项，支持新增/删除/上下移；经 `InterfaceSchemaSerializer` 回写（修复旧版按行 key 匹配导致参数值被丢弃的 bug） |
| 21 | **公式编辑器** | 可视化编辑 Calc 公式：函数目录（`FormulaCatalog`）+ 语法校验（`FormulaValidator` 带错误位置）+ 试算 + 经 `FormulaFieldSerializer` 回写 `[Measure*] Formula=`；同时补全 `DefaultFormulaEngine.GetFunctions()` 元数据 |

---

## 待开发 📋

> 以下均为「已完成 ✅」列表之外、尚未落地的功能。已删除与「已完成」重复的条目（P0 全部、P1 #13/15/16、P2 #17/19）。

### P1 — 内容扩展

| # | 功能 | 复杂度 | 说明 |
|---|------|--------|------|
| 14 | **图标格子 embed** | 高 | 桌面图标嵌入（✅ 已完成：IconGridEmbedComponent + `INativeDesktop.GetDesktopIcons()` + `WpfImage` 图标渲染 + 点击打开，样例 `Assets/Samples/icon-grid.pri`） |

### P2 — 编辑器 & 工具

| # | 功能 | 复杂度 | 说明 |
|---|------|--------|------|
| 18 | **Studio 实时预览** | 中 | 编辑 `.pri` 时预览实时刷新（✅ 已完成：编辑即预览防抖重建 + 启动即显示预览） |
| 20 | **参数 Schema 设计器** | 高 | 可视化定义 `[Interface.*]` 参数类型/默认值（✅ 已完成：Studio 卡片式设计器 + `InterfaceSchemaSerializer` 纯逻辑回写 + 5 项单测） |

### P3 — 高级特性

| # | 功能 | 复杂度 | 说明 |
|---|------|--------|------|
| 21 | **公式编辑器** | 高 | 可视化编辑 Calc 公式（✅ 已完成：函数目录 + 语法校验 + 试算 + `FormulaFieldSerializer` 回写 + 8 项单测） |
| 22 | **动画系统** | 高 | 过渡动画、缓动函数（✅ 已完成：`.pri` 声明式 `[Animation*]` 语法 + `AnimationSpec`/`NamedEasing`/`AnimationSpecSerializer` 纯逻辑 + `ComponentAnimator` 运行时 + Studio「动画」Tab 设计器 + 11 项单测） |
| 23 | **主题/皮肤** | 中 | 一键切换组件主题（✅ 已完成：`.pri` 声明式 `[Theme.*]` 调色板 + `@Theme.Key` 引用 + `Theme=` 选择 + `ThemeCatalog`/`ThemeResolver` 纯逻辑 + Studio「主题」Tab 设计器 + 8 项单测；Desktop 右键"切换主题"已联动重渲染） |
| 24 | **多屏差异化** | 中 | 每屏独立组件集（✅ 已完成：`.desktop.profile` 格式 `[Desktop] Default=` + `[Screen.*]`（匹配键 Primary/Secondary/数字索引/设备名子串）；`ScreenProfileCatalog` 纯逻辑解析/Resolve/Validate/ToText + 12 项单测；Desktop 默认模式按配置为每屏加载各自组件（含热加载仅重载引用该 `.pri` 的窗口）；Studio「多屏」Tab 可视化编辑并回写 `AppData\Prismica\desktop.profile`） |
| 25 | **AI 辅助生成（文档化）** | 高 | 用户用任意 AI Agent 自写/改组件：权威 `.pri` 格式文档 + 可直接丢给 AI 的提示词模板（创建/修改/诊断）+ 可运行示例（✅ 已完成：`docs/AI_COMPONENT_AUTHORING.md` + `docs/examples/clock-cpu-theme.pri`；按用户澄清落地为"赋能用户侧 AI 创作"，非内置生成器） |

### P4 — 稳定性 & 发布

| # | 功能 | 复杂度 | 说明 |
|---|------|--------|------|
| 26 | **自动更新** | 中 | 检查新版本、提示重启（✅ 已完成：Core 纯逻辑 `SemVersion`/`UpdateManifest`/`UpdateDecision`（版本解析比较、清单 JSON 解析、升级决策含渠道/预发布/最低版本强制）+ 32 项单测；App 层 `IUpdateSource`/`HttpUpdateSource`/`NoOpUpdateSource`/`UpdateChecker`（仅检查+通知、不静默下载以免覆盖运行中的程序）；`DesktopOptions` 加 `UpdateUrl`/`UpdateChannel`/`UpdateIncludePrerelease`/`CheckUpdateOnStartup`；CompositionRoot 注册当前版本（入口程序集 InformationalVersion）；托盘新增"Check for Updates"、启动后延迟 15s 自动检查；未配置 UpdateUrl 时走 NoOp 不检查） |
| 27 | **崩溃上报** | 中 | 崩溃日志上传、远程诊断（✅ 已完成：结构化 JSON 崩溃报告 `CrashReport`/`CrashReportBuilder`/`LocalCrashSink` 纯逻辑 + 11 项单测；App 层 `ICrashSink`/`HttpCrashSink`/`LocalCrashSinkAdapter`/`CrashReporter`；`Program` 全局捕获（顶层 + `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`）落盘 `crashes/crash_*.json`，可选 HTTP 上报（配 `CrashReportUploadUrl`）；`DesktopOptions` 加 `CrashReportEnabled`/`CrashReportUploadUrl`；保留原重启兜底） |
| 28 | **性能优化** | 中 | 渲染帧率自适应、内存占用优化（✅ 已完成：`FrameRateGovernor` 纯逻辑——按 `RenderActivity`(活跃动画/脏标记/实时meter) 在 活跃60/存活2/空闲1 fps 间切换并带迟滞；`IFrameScheduler.ActiveAnimationCount` 驱动接线；`StartRuntimeLoop` 空闲帧跳过 `UpdateAsync`+`InvalidateVisual` 降 CPU 与分配；含 11 项单测） |
| 29 | **单元测试补全** | 中 | 覆盖 parser/runtime/meter/embed（✅ 已完成：新增 52 项单测覆盖 `ThemeCatalog`/`AnimationSpecSerializer`/`Easing`+`NamedEasing`/`FormulaValidator`+`FormulaFieldSerializer`/`ComponentAnimator`/`Primitives`(ArgbColor/Rect/Size/Point/Thickness/CornerRadius/Matrix3x2/Transform)；总测试 264/264 全绿（Core 245 + Infra 19）） |
| 30 | **安装包** | 低 | MSI/Inno Setup 打包（✅ 已完成：Inno Setup 脚本 `build/installer.iss` + 一键发布脚本 `build/Publish.ps1`——读 `Directory.Build.props` 版本 → 单文件/自包含/win-x64 发布 Desktop+Studio → 预置 `Components/ClockCpu.pri`(对应内置默认 profile) 与 `Docs/` → 调 `iscc` 编译安装包（未装则跳过并提示手动命令）；`Directory.Build.props` 发布段关闭 `PublishReadyToRun`（单文件+R2R 组合部分环境触发 NETSDK1094）；首次发布需 `dotnet restore -r win-x64` 注入 win-x64 还原图） |

---

## 当前开发重点

**真实待开发（已完成列表之外）：** 无——P1–P4（#1–#30）全部完成。

#1–#25 核心功能、#26 自动更新、#27 崩溃上报、#28 性能优化、#29 单元测试补全、**#30 安装包** 均已完成。**项目进入可发布状态**。

**发布工程新增（2026-09-04 下午，P4 收尾）**：
- **CI/CD 流水线**：`.github/workflows/ci.yml`（任意 push/PR → restore+build+test，264 项全过才放行）+ `.github/workflows/release.yml`（打 `v*` tag / 手动触发 → 安装 Inno Setup → `Publish.ps1` 出单文件安装包 → 可选签名 → 上传产物 + 建 GitHub Release）。
- **代码签名**：`build/Publish.ps1` 新增 `-Sign`（Authenticode，`signtool` + 时间戳），支持 PFX(`-CertFile`/`-CertPassword`) 或本机证书指纹(`-CertThumbprint`)；release 工作流通过 `SIGNING_CERT_PFX`/`SIGNING_CERT_PASSWORD` secret 解码并调用；`docs/CI_CD.md` 另含 **Azure Trusted Signing** 免证书方案。`dist/` 已加入 `.gitignore`。
- 注意：GitHub Actions 需先把仓库初始化并推到 GitHub（当前项目**还不是 git 仓库**），详见 `docs/CI_CD.md` 启用步骤。沙箱无法端到端跑 Actions；`Publish.ps1` 发布链路此前已在 #30 验证。

后续可选项：自动更新服务器、官方文档站，或 P5 新功能迭代。

---

## P5 新功能（进行中）

### #31 动态壁纸层（路线 B：桌面插入 + 透明度穿透）
- **路线 B**：壁纸窗口不置顶，经 `NativeMethods.InsertAboveDesktop` 注入到 Progman/WorkerW 之上、普通窗口之下（经典 live-wallpaper 手法：`SendMessage(Progman,0x052C)` 生成桌面 WorkerW → `EnumWindows` 找承载 `SHELLDLL_DefView` 的 WorkerW → `SetParent` + `HWND_BOTTOM`）。
- **透明度穿透（组件模式）**：`WallpaperLayerWindow`（Infra.Wpf）复用组件窗口的 `WM_NCHITTEST` 内容矩形判定——透明空区→`HTTRANSPARENT`（穿透到下层桌面），命中壁纸内容（meter 布局区）→`HTCLIENT`（接收）。`SetClickThrough(true)` 可切整窗穿透。
- **透明度穿透（图片模式，新增）**：`Mode=Image` + `ImagePath` 指向带 alpha 的 PNG。`WallpaperLayerWindow.SetImage` 加载图片并以 Fill 铺满虚拟桌面，同时一次性扫描 PNG alpha 通道构建 `AlphaMask`（预识别缓存，`PngAlphaMask.BuildFromPng`）。命中测试将客户坐标映射到图片像素查遮罩：alpha<=Threshold（默认 0 = 仅完全透明）的像素穿透、其余接收点击——即"逐像素识别透明区生成形状遮罩"。
- **纯逻辑**：`Core/Wallpaper/WallpaperHitTest.IsTransparent(point, contentRects)`（组件矩形判定）+ `Core/Wallpaper/AlphaMask`（图片逐像素遮罩，O(1) 查表）。二者均单测覆盖。
- **接线**：`DesktopHostedService.CreateWallpaperLayer` 在 `RunUi` 早期创窗口；`Mode=Image` 走 `SetImage` 图片模式（无 runtime，静态壁纸），否则走组件模式（共享帧调度）。配置 `Prismica:Desktop:Wallpaper: { Enabled, Mode(Component|Image), Path, ImagePath }`。
- 示例：`docs/examples/wallpaper.pri`（组件模式，透明背景 + 角落时钟/CPU）。
- 验证：沙箱 `dotnet build/test --no-restore` 0 错误 / 276/276 全绿（Core 257 + Infra 19；新增 7 项 AlphaMask 单测）。**win32 窗口注入与图片逐像素穿透需本机肉眼确认**（沙箱无桌面会话）。

## #32 封装接口 ↔ 内容变量桥接（阶段 0+1，已完成）
- **需求来源**：早期架构 §7 参数接口 `[Interface.*]` + §9 实例覆盖；用户澄清「Studio 只创建组件并留出封装接口、布局模式拉起窗口修改变量与尺寸」。此前 `IniLayoutSerializer` 已把实例 `Interface.*` 写入 `layout.ini`，但**运行时从未注入**（`UpdateVisual` 被定义却无人调用、且只处理 opacity；`RenderContext.GlobalVariables` 一直只用组件自身 `[Variables]`），即"打折扣"缺口。
- **核心逻辑**：`Core/Parameters/InterfaceBinder.ResolveVariables(schema, overrides, baseVariables)` —— 仅把「隐式变量绑定的颜色类」Interface 参数（IsImplicitVariableBinding，或 Color 类型且无显式 ApplyTo）按 `#Var#` 机制合并进颜色变量字典；取值优先级：实例 override > 参数 DefaultValue > 基础变量；解析失败回退基础变量；非颜色/显式 ApplyTo 路径留给后续属性面板阶段。附 `TryParseColor`（hex / R,G,B / R,G,B,A）。
- **接线**：`DesktopHostedService` 布局分支（原 `RenderContext` 直接用 `def.Variables`）改为先经 `InterfaceBinder` 合并实例 `inst.ParameterOverrides` 再构造 `RenderContext`——实现"布局模式改变量，呈现模式实时生效"。多屏分支无实例覆盖，保持 `def.Variables`。
- **单元测试**：`tests/.../Parameters/InterfaceBinderTests.cs`（6 项：覆盖优先、非颜色/ApplyTo 忽略、缺省回退、解析失败回退、空 schema/overrides、TryParseColor 格式）。
- **验证**：沙箱 `dotnet build/test --no-restore` 0 错误 / 282/282 全绿（Core 263 + Infra 19；+6 项）。

## #33 双视图模式 + 属性面板（阶段 2+3，已完成，需本机肉眼验证）
- **设计对齐**（用户确认，与 lumen/Sapphire/Rainmeter 一致）：Desktop 本体自带「呈现 / 布局」双模式切换（托盘菜单 + `Ctrl+Alt+E`）；Studio 只做组件创作入库。
- **已完成**：
  - **纯逻辑**：`Core/Desktop/DesktopViewMode.cs`（`DesktopViewMode` 枚举 + `DesktopViewModeRules`：Toggle/ShouldClickThrough/ToLabel/Parse，可单测）。
  - **WPF**：`WpfOverlayWindow` 增加 `OnToggleViewMode` 事件 + `Ctrl+Alt+E` 快捷键 + `IsSelected` 选中边框。
  - **App**：`ComponentPropertyWindow`（数据驱动——列出实例 `[Interface]` schema 生成控件 + 尺寸 X/Y/W/H 数值输入；颜色做 hex 校验；应用回调写回 layout 实例 → `ReloadInstanceOverlay` 经 `InterfaceBinder` 注入变量 → 实时重渲染 → `SaveLayout`）；`CreateOverlay` 统一实例窗口创建（按视图模式设穿透、注册切换事件、存 `_overlayInstances`）；`ToggleViewMode`（遍历窗口改 click-through）；`ShowPropertyWindow`/`RemoveComponentLive`/`AddComponentToLayout`（接通了原 `add/remove/settings` 的 TODO，支持运行时增删与多实例）；`ReloadInstanceOverlay` 重建单实例窗口。
  - **配置**：`DesktopOptions.ViewMode`（默认 Desktop）+ `appsettings.json` 的 `Prismica:Desktop:ViewMode`；托盘"Toggle Layout Mode"菜单项订阅 `ToggleViewMode`。
- **单元测试**：`tests/.../Desktop/DesktopViewModeTests.cs`（覆盖 Toggle/ShouldClickThrough/Parse/ToLabel）。
- **实例模型**：支持"同组件多实例各自覆盖"（layout 实例按 `ComponentName` 多实例物化）。
- **本机待验证**（沙箱无桌面会话）：托盘/快捷键切换双模式、布局模式下可否选中并改属性、改变量/尺寸即时重渲染、增删组件即时生效、`Ctrl+Alt+E` 快捷键。

## #34 全局变量跨组件共享 gv:（已完成）
- **需求来源**：早期架构 §6/F9——组件 A 写入 `gv:Name`，组件 B 经 `#gv:Name#` 实时读取；全局变量需跨组件共享（此前 `RenderContext.GlobalVariables` 只用组件自身 `[Variables]`，无跨组件通道）。
- **核心逻辑**：
  - `Core/Parameters/GlobalVariableStore.cs`：跨组件共享字符串字典（`Get`/`Set`/`TryAdd`/`Remove`/`Clear`/`Snapshot`；大小写不敏感键），实现 `IReadOnlyDictionary<string,string>` 以便直接注入 `MeterContext.Globals`。
  - 声明式 seed：`.pri` 新增 `[GlobalVariables]` 段（键值对字符串初值），加载时经 `IniSkinTextParser.ParseGlobalVariables` 解析进 `ComponentDefinition.GlobalVariables`；`ComponentRuntime.Create` 用 `TryAdd` 把初值 seed 进**共享**存储（仅当变量尚不存在，避免覆盖运行期变更）。
  - 运行期写：`GlobalVariableStore.Set(name,value)` 供 embed/脚本随时写入；下一次渲染中 `#gv:Name#` 即反映（实时联动）。
- **接线**：`DesktopHostedService` 持有单例 `_globals`（一个 `GlobalVariableStore` 实例），传入所有 `ComponentRuntime.Create` 调用——故桌面端所有组件共享同一存储，实现真正跨组件读写。`MeterContext` 新增第 6 位参数 `Globals`；`StringMeter.UpdateAsync` 在 `#Var#`（组件自身颜色变量）之外，新增 `#gv:Name#` 走共享 Globals 字符串替换。
- **单元测试**：`tests/.../Parameters/GlobalVariableTests.cs`（5 项：读写/跨实例共享/声明式 seed/缺失返回 null/清空）。
- **验证**：沙箱 `dotnet build/test --no-restore` 0 错误 / 300/300 全绿（Core 281 + Infra 19；+5 项 F9 测试）。跨组件共享逻辑已由单测覆盖（同 store 实例两组件读写一致）；无需额外本机验证（共享 store 为进程内，非 win32 仅桌面会话相关）。

## #35 MeterStyle 继承（已完成）
- **需求来源**：早期架构未交付项「MeterStyle 继承」。`.pri` 用 `[MeterStyle*]` / `[Style*]` 段定义命名样式，meter 段用 `MeterStyle=Name1,Name2`（或 `Style=`）引用；引用样式的字段合并进 meter 自身字段（meter 自身覆盖样式）。此前 `IMeter.Style`/`MeterStyle` 记录是死脚手架——`ComponentRuntime.Create` 从不消费 `def.Styles`、不解析引用、不合并；且 `ParseMeters` 有 bug：`[MeterStyle*]` 被 `StartsWith("Meter")` 误当 meter 捕获，产生幽灵 StringMeter。
- **纯逻辑**：`Core/Styling/MeterStyleResolver.cs`：`Resolve(meterFields, styles)` —— 合并优先级 样式按引用顺序排列（后者覆盖前者）< meter 自身字段（最高）；支持样式嵌套引用（样式自身含 `MeterStyle=`）+ 环检测；键大小写不敏感；引用键（MeterStyle/Style）不进入合并结果；返回 `StyleResolutionResult(MergedFields, ParentStyles, MissingStyles)`。
- **接线**：`IniSkinTextParser.ParseMeters` 跳过 `[MeterStyle*]`（修复误吞 bug）；`ComponentRuntime.Create` 构造 meter 前 `MeterStyleResolver.Resolve(m.Fields, def.Styles)` → `meter.Style = new MeterStyle(...)` + `meter.Configure(merged)`；缺失样式名记入 `Diagnostics`。`def.Styles` / `StyleDefinition` 早已存在，仅补齐解析与合并。
- **单元测试**：`tests/.../Styling/MeterStyleTests.cs`（8 项：单样式合并+覆盖/多样式后者覆盖+自身胜/嵌套继承/未知样式报告+自身仍生效/环检测不死循环/无引用等于自身/大小写不敏感/解析器+解析器产出端到端）+ `tests/.../App/ComponentRuntimeTests.cs` 2 项运行时断言（meter.Style 反映合并、未知样式诊断）。
- **验证**：沙箱 `dotnet build/test --no-restore` 0 错误 / 310/310 全绿（Core 289 + Infra 21；+10 项 MeterStyle 测试）。纯解析+运行时构造，无需桌面会话，已单测覆盖。`.pri` 语法：`[MeterStyleTitle] FontColor=#FFFF0000` + `[MeterTitle] Meter=String MeterStyle=Title FontColor=#FF00FF00`（后者覆盖前者）。

## #37 动态壁纸 GIF/MP4 支持（已完成）
- **需求来源**：用户指出 `#31 动态壁纸层` 实际只支持 PNG，真要"动态"还需 GIF/MP4。约束：**GIF/MP4 不预计算 alpha 遮罩**（与 PNG 的逐像素遮罩穿透不同）。
- **纯逻辑**：`Core/Wallpaper/WallpaperMediaKind.cs`：`WallpaperMediaKind` 枚举（Png/Gif/Video）+ `WallpaperMediaKindExtensions.FromPath(path)` 按扩展名识别（png→Png；gif→Gif；mp4/webm/avi/mkv/mov/m4v→Video；未知→Png 向后兼容）。
- **接线**：`WallpaperLayerWindow` 新增 `SetMedia(path, virtualBounds)` 统一入口按 Kind 分派；`SetGif`（GifBitmapDecoder 取帧 + DispatcherTimer 按每帧 `/grctlext/Delay` 元数据换帧，单帧直显）+ `SetVideo`（MediaElement 全屏循环播放，MediaEnded→Position=Zero）；GIF/视频**不设 `_mask`、不置 `_root`** → `WM_NCHITTEST` 在无遮罩/无组件根时默认返回 HTTRANSPARENT，整窗点击穿透（无需预计算遮罩）；`SetImage`(PNG+alpha 遮罩) 路径完全不变。`DesktopHostedService.CreateWallpaperLayer` 改调 `SetMedia`；`Dispose` 补 `_gifTimer.Stop()` + `_media.Close()`。
- **单元测试**：`tests/.../Wallpaper/WallpaperMediaKindTests.cs`（10 项扩展名→Kind 理论测试，纯逻辑、无 WPF）。
- **验证**：沙箱 `dotnet build/test --no-restore` 0 错误 / 326/326 全绿（Core 304 + Infra 22；+10 项媒体类型测试）。GIF/MP4 真实播放需 MediaFoundation + 桌面会话，沙箱仅编译验证，待用户本机验证。`.desktop` 配置：`Wallpaper:Mode=Image` + `Wallpaper:ImagePath=xxx.gif|xxx.mp4`。

## #38 Internal Z-Index 运行时层级（已完成）
- **需求来源**：用户问"同位置下多个组件是否可用 Internal Z-Index 区分层级"。调研发现 `ComponentInstance.ZIndex` 字段已存在且序列化（`IniLayoutSerializer` 读写 `ZIndex=`），但运行时从未消费——`SetZOrder(IntPtr)` 在 `WpfOverlayWindow`/`WallpaperLayerWindow` 均有定义却**从未被调用**，覆盖窗口实际按创建顺序叠放。
- **纯逻辑**：`Core/Layout/ZOrderArranger.cs`：`Order(IEnumerable<(IOverlayWindow Window, int ZIndex)>)` 升序稳定排序（ZIndex 升序、相同保创建顺序），返回应按 `SetZOrder(HWND_TOP)` 调用的窗口序列（最高 ZIndex 最后→置顶）。
- **接线**：`DesktopHostedService` 新增 `ApplyZOrder()`——取所有覆盖窗口及其实例 ZIndex（无实例取 0），按 `ZOrderArranger.Order` 升序依次 `SetZOrder(IntPtr.Zero)`（HWND_TOP，在 TOPMOST 段内逐置顶）；调用点：实例加载循环后、`AddComponentToLayout` 末尾、`RemoveComponentLive` 末尾。壁纸层非 TOPMOST，不参与。
- **单元测试**：`tests/.../Layout/ZOrderArrangerTests.cs`（4 项：升序 ZIndex→调用次序、同 ZIndex 保序、null 输入空、模拟 ApplyZOrder 调用语义最高 ZIndex 最后置顶）。
- **验证**：沙箱 326/326 全绿（+4 项）。真实置顶需桌面会话，沙箱仅验证排序逻辑，待用户本机验证。同位置多组件现可经 `layout.ini` 的 `ZIndex=` 或属性面板（规划中）区分前后层级。

## 关键文件地图

| 模块 | 路径 | 说明 |
|------|------|------|
| Core | `src/Prismica.Core/` | 纯逻辑，无 WPF |
| App | `src/Prismica.App/` | Composition root，DesktopHostedService |
| Wpf | `src/Prismica.Infra.Wpf/` | WPF 桥接（WpfVisualRoot/WpfOverlayWindow） |
| Native | `src/Prismica.Infra.Native/` | Win32 桌面/屏幕/图标 |
| Embeds | `src/Prismica.Infra.Embeds/` | Embed 组件（Clock） |
| Desktop | `src/Prismica.Desktop/` | 入口 Program.cs |
| Studio | `src/Prismica.Studio/` | 编辑器 |

## 构建/测试

```powershell
dotnet build Prismica.sln -c Debug    # 0 错误
dotnet test Prismica.sln -c Debug     # 326/326 全绿（Core 304 + Infra 22）
```

> 沙箱/CI 环境注意：若 `dotnet` 进程无法解析机器级 NuGet 配置目录（`ProgramData` 等特殊文件夹为空）导致 restore 报 `Value cannot be null (path1)`，用 `dotnet build/test ... --no-restore`（项目已有本地 `obj/project.assets.json` 与 nuget 缓存）。真实 Windows 上正常 restore。
