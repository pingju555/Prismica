# Prismica

> Windows 桌面小部件引擎 · A Windows desktop widget engine

Prismica 是一个基于 **.NET 8 + WPF** 构建的 Windows 桌面小部件引擎，设计灵感来自 KLWP（Kustom Live Wallpaper）与 Rainmeter。你用一套声明式的 `.pri` 文本格式描述组件（时钟、CPU 进度条、天气、音乐控制、便签、桌面图标格、动态壁纸等），引擎在桌面之上以**透明覆盖窗口**渲染，并支持点击穿透、多组件叠加、主题换肤、动画、多屏差异化、全局变量与样式继承。

Prismica is a Windows desktop widget engine built with **.NET 8 + WPF**, inspired by KLWP (Kustom Live Wallpaper) and Rainmeter. You describe widgets (clock, CPU bar, weather, music control, sticky note, desktop icon grid, live wallpaper, …) in a declarative `.pri` text format; the engine renders them as **transparent overlay windows** on the desktop, with click-through, multi-widget stacking, theming, animation, per-screen layouts, global variables, and style inheritance.

- 中文文档见 [`docs/`](docs/)；组件创作权威指南见 [`docs/AI_COMPONENT_AUTHORING.md`](docs/AI_COMPONENT_AUTHORING.md)。
- Chinese docs live in [`docs/`](docs/); the authoritative component-authoring guide is [`docs/AI_COMPONENT_AUTHORING.md`](docs/AI_COMPONENT_AUTHORING.md).

---

## 目录 / Table of Contents

- [特性 / Features](#特性--features)
- [架构 / Architecture](#架构--architecture)
- [快速上手 / Quick Start](#快速上手--quick-start)
- [组件格式 `.pri` / The `.pri` Component Format](#组件格式-pri--the-pri-component-format)
- [构建与测试 / Build & Test](#构建与测试--build--test)
- [安装与发布 / Install & Release](#安装与发布--install--release)
- [许可证 / License](#许可证--license)

---

## 特性 / Features

| 类别 Category               | 能力 Capability                                                                                                                                                                                                                                                                       |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 桌面运行时 Desktop runtime     | 透明覆盖窗口 + 点击穿透（空区穿透到桌面，内容区可交互）；多组件叠加（每实例独立窗口/位置/大小，全部同步更新）<br>*Transparent overlay + click-through (empty areas pass clicks to the desktop, content is interactive); multi-widget stacking (each instance is an independent window with its own position/size, all updated in sync)* |
| 窗口管理 Windowing            | 顶部拖拽手柄移动、8px 缩放边框、右键上下文菜单、布局持久化（`layout.ini`）+ 每 5s Checkpoint 异常恢复<br>*Top drag handle, 8px resize border, context menu, layout persistence (`layout.ini`) + 5s checkpoint recovery*                                                                                               |
| 稳定性 Stability             | 单实例 Mutex + 崩溃日志 + 自动重启；全局异常捕获落盘 `crashes/crash_*.json`，可选 HTTP 上报<br>*Single-instance Mutex + crash log + auto-restart; global exception capture to `crashes/crash_*.json`, optional HTTP upload*                                                                                  |
| 托盘与自启 Tray & autostart    | 托盘图标（Open Studio / 切换主题 / Check for Updates / Exit）；Startup 文件夹开机自启<br>*Tray icon (Open Studio / Toggle Theme / Check for Updates / Exit); Startup-folder autostart*                                                                                                                |
| 主题与动画 Theming & animation | 声明式 `[Theme.*]` 调色板 + `@Theme.Key` 引用 + `Theme=` 一键换肤；`[Animation*]` 声明式动画（OnShow/OnClick/定时），28 个缓动函数<br>*Declarative `[Theme.*]` palettes + `@Theme.Key` refs + `Theme=` switch; declarative `[Animation*]` (OnShow/OnClick/timed) with 28 easings*                               |
| 多屏 Multi-screen           | 每屏按 `.desktop.profile` 加载各自组件集（匹配键 Primary/Secondary/数字索引/设备名子串）<br>*Per-screen component sets via `.desktop.profile` (match keys: Primary/Secondary/index/device-name substring)*                                                                                                  |
| 动态壁纸 Live wallpaper       | 路线 B：壁纸窗口注入桌面图标层之下；组件模式（内容区可点击）+ 媒体模式（`Mode=Image`，按扩展名自动识别：PNG 走逐像素 alpha 遮罩穿透；GIF 逐帧动画、MP4/WebM/AVI 视频全屏循环播放，**整窗点击穿透、不预计算遮罩**）<br>*Route B: wallpaper window below the desktop icon layer; Component mode (clickable content) + Media mode (`Mode=Image`, auto-detected by extension: PNG pixel-alpha mask; GIF frame animation, MP4/WebM/AVI video full-screen loop — both click-through with no precomputed mask)* |
| 性能 Performance            | `FrameRateGovernor` 按活动状态在 60/2/1 fps 间自适应切换并带迟滞；空闲帧跳过渲染更新<br>*`FrameRateGovernor` adapts 60/2/1 fps by activity with hysteresis; idle frames skip render updates*                                                                                                                  |
| 全局变量 Global vars          | `[GlobalVariables]` 声明初值；`#gv:Name#` 跨组件实时共享字符串（桌面单例 `GlobalVariableStore`）<br>*`[GlobalVariables]` seed values; `#gv:Name#` cross-widget shared strings (desktop-singleton `GlobalVariableStore`)*                                                                                 |
| 样式继承 MeterStyle           | `[MeterStyle*]` 命名样式，meter 用 `MeterStyle=Name1,Name2` 引用并按优先级合并（自身覆盖样式），支持嵌套与环检测<br>*`[MeterStyle*]` named styles; meters reference via `MeterStyle=Name1,Name2` and merge by priority (self overrides), with nesting + cycle detection*                                            |
| Studio 编辑器 Editor         | WPF 所见即所得：实时预览、组件库、参数 Schema 设计器、公式编辑器（函数目录+语法校验+试算）、动画/主题/多屏 Tab、属性面板<br>*WPF WYSIWYG: live preview, component library, interface-schema designer, formula editor (catalog + validation + evaluate), animation/theme/multi-screen tabs, property panel*                            |
| 组件生态 Ecosystem            | Embeds：Clock / Music（SMTC）/ Weather（wttr.in）/ StickyNote / IconGrid；AI 辅助创作文档 + 提示词模板<br>*Embeds: Clock / Music (SMTC) / Weather (wttr.in) / StickyNote / IconGrid; AI authoring docs + prompt templates*                                                                           |
| 工程发布 Engineering          | 311/311 单元测试；Inno Setup 安装包 + 一键发布脚本；GitHub Actions CI/CD + Authenticode / Azure Trusted Signing 代码签名<br>*311/311 unit tests; Inno Setup installer + one-click publish script; GitHub Actions CI/CD + Authenticode / Azure Trusted Signing*                                         |

---

## 架构 / Architecture

分层设计：纯逻辑（无 WPF）在底层，WPF / Win32 桥接在高层，双入口（`Desktop` 桌面引擎、`Studio` 编辑器）。

Layered design: pure logic (no WPF) at the bottom, WPF / Win32 bridges above, two entry points (`Desktop` engine, `Studio` editor).

| 层 Layer | 项目 Project | 职责 Responsibility |
|---|---|---|
| Core | `src/Prismica.Core` | 纯逻辑：解析、度量、meter、主题、动画、公式、更新/崩溃决策、全局变量、样式解析（无 WPF）<br>*Pure logic: parsing, measures, meters, theming, animation, formula, update/crash decisions, global vars, style resolution (no WPF)* |
| App | `src/Prismica.App` | 组合根：把 Core/Infra 接线成运行时（`ComponentRuntime`、`DesktopHostedService`）<br>*Composition root: wires Core/Infra into a runtime* |
| Infra | `Prismica.Infra` (+ `.Wpf`/`.Native`/`.Embeds`/`.Measures`/`.Meters`) | WPF 桥接、Win32 桌面/屏幕/图标、Embed 组件、度量/meter 实现<br>*WPF bridge, Win32 desktop/screen/icons, embed components, measure/meter implementations* |
| Ui | `src/Prismica.Ui` | 共享 UI 原语：转换器、行为、附加属性、主题令牌、基类 ViewModel、预览画布<br>*Shared UI primitives: converters, behaviors, attached props, theme tokens, base ViewModel, preview canvas* |
| 入口 Entries | `src/Prismica.Desktop`、`src/Prismica.Studio` | WinExe 入口：`Desktop` 桌面引擎、`Studio` 编辑器<br>*WinExe entries: `Desktop` engine, `Studio` editor* |

```
Prismica.sln
├─ src/Prismica.Core        (pure logic, no WPF)
├─ src/Prismica.App         (composition root)
├─ src/Prismica.Infra*      (Wpf / Native / Embeds / Measures / Meters)
├─ src/Prismica.Ui          (shared UI primitives)
├─ src/Prismica.Desktop     (engine entry, WinExe)
└─ src/Prismica.Studio      (editor entry, WinExe)
```

---

## 快速上手 / Quick Start

### 前置要求 / Prerequisites

- Windows 10 / 11（覆盖窗口与点击穿透依赖 Win32 桌面 API）
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 / 11 (overlay + click-through rely on Win32 desktop APIs)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 构建与运行 / Build & Run

```powershell
# 还原 + 构建（Debug）
dotnet build Prismica.sln -c Debug

# 运行桌面引擎（在桌面之上渲染 Components/ 下的 .pri 组件）
dotnet run --project src/Prismica.Desktop -c Debug

# 运行 Studio 编辑器（所见即所得创作组件）
dotnet run --project src/Prismica.Studio -c Debug
```

将 `.pri` 文件放入程序目录下的 `Components/` 即可被 Desktop / Studio 加载（示例见 [`docs/examples/`](docs/examples/)）。
Drop `.pri` files into the `Components/` folder next to the executable to load them (see [`docs/examples/`](docs/examples/)).

---

## 组件格式 `.pri` / The `.pri` Component Format

`.pri` 是一套类 INI 的声明式格式，由 `IniSkinTextParser` 解析为 `ComponentDefinition`。主要节：

`.pri` is an INI-like declarative format parsed by `IniSkinTextParser` into a `ComponentDefinition`. Key sections:

| 节 Section | 作用 Purpose |
|---|---|
| `[Prismica]` | 元信息：`Name`（组件名）、`Version`、`Width`/`Height`、`Theme`、`Update`（刷新毫秒）<br>*Metadata: `Name`, `Version`, `Width`/`Height`, `Theme`, `Update` (refresh ms)* |
| `[Variables]` | 组件私有颜色变量（`#Var#` 引用）<br>*Component-private color vars (`#Var#` refs)* |
| `[GlobalVariables]` | 跨组件共享字符串初值（`#gv:Name#` 引用）<br>*Cross-widget shared string seeds (`#gv:Name#` refs)* |
| `[Interface.*]` | 可在 Studio 可视化配置的参数（Number/Color/Bool/Select/Text）<br>*Parameters configurable in Studio* |
| `[Measure*]` | 数据度量：`Time`/`CPU`/`Calc`(`Formula=`)/`WebParser` 等<br>*Data measures: `Time`/`CPU`/`Calc`(`Formula=`)/`WebParser`…* |
| `[Meter*]` | 视觉元素：`String`/`Progress`/`Image`…；`MeterStyle=` 引用命名样式<br>*Visual elements: `String`/`Progress`/`Image`…; `MeterStyle=` refs named styles* |
| `[Animation*]` | 声明式动画：触发器/目标/属性/缓动/时长<br>*Declarative animation: trigger/target/property/easing/duration* |
| `[Theme.*]` | 调色板令牌，`@Theme.Key` 引用<br>*Palette tokens, `@Theme.Key` refs* |
| `[MeterStyle*]` | 命名样式，供 meter 的 `MeterStyle=` 引用并合并<br>*Named styles referenced & merged by meters* |

### 最小示例 / Minimal example

```ini
[Prismica]
Name=ClockCpu
Version=0.1
Width=260
Height=96
Theme=Dark

[Variables]
Text=@Theme.Text
Accent=@Theme.Accent

[MeasureTime]
Measure=Time
Format=%H:%M:%S

[MeterClock]
Meter=String
MeasureName=MeasureTime
X=0 Y=0 W=260 H=46
FontSize=36
FontColor=@Theme.Text

[Theme.Dark]
Text=#FFF3F3F3
Accent=#FF4C8BF5
```

完整可运行示例见 [`docs/examples/clock-cpu-theme.pri`](docs/examples/clock-cpu-theme.pri)（含进度条、动画、明暗主题切换）与 [`docs/examples/wallpaper.pri`](docs/examples/wallpaper.pri)（动态壁纸层）。
Full runnable examples: [`docs/examples/clock-cpu-theme.pri`](docs/examples/clock-cpu-theme.pri) and [`docs/examples/wallpaper.pri`](docs/examples/wallpaper.pri).

---

## 构建与测试 / Build & Test

```powershell
# 构建（Debug，0 错误）
dotnet build Prismica.sln -c Debug

# 运行测试（当前 311/311 全绿：Core 289 + Infra 22）
dotnet test Prismica.sln -c Debug
```

> **沙箱 / CI 提示 / Sandbox / CI note**：某些受限环境（如 `ProgramData` 等环境变量为空）下 NuGet 还原会报 `Value cannot be null (path1)`。此时用 `--no-restore` 复用本地 `obj/project.assets.json` 与 NuGet 缓存即可：
> *In some restricted environments NuGet restore fails with `Value cannot be null (path1)`. Use `--no-restore` to reuse the local `obj/project.assets.json` and NuGet cache:*
> ```powershell
> dotnet build Prismica.sln -c Debug --no-restore
> dotnet test  Prismica.sln -c Debug --no-restore
> ```

---

## 安装与发布 / Install & Release

- **安装包 / Installer**：`build/installer.iss`（Inno Setup）+ `build/Publish.ps1`（读版本 → 单文件/自包含/win-x64 发布 Desktop+Studio → 预置 `Components/` 与 `Docs/` → 调 `iscc` 生成安装包）。
  *`build/installer.iss` (Inno Setup) + `build/Publish.ps1` (version → single-file/self-contained/win-x64 publish → preset `Components/` & `Docs/` → `iscc` build).*
- **代码签名 / Signing**：`build/Publish.ps1 -Sign`（Authenticode，`signtool` + RFC3161 时间戳），支持 PFX 或本机证书指纹；亦支持 Azure Trusted Signing 免证书方案（见 `docs/CI_CD.md`）。
  *`build/Publish.ps1 -Sign` (Authenticode + RFC3161 timestamp), PFX or local thumbprint; Azure Trusted Signing also documented in `docs/CI_CD.md`.*
- **CI/CD**：`.github/workflows/ci.yml`（push/PR → restore+build+test 全绿才放行）；`.github/workflows/release.yml`（打 `v*` tag 或手动 → 出安装包 → 可选签名 → 建 GitHub Release）。
  *`.github/workflows/ci.yml` (push/PR gate) + `.github/workflows/release.yml` (`v*` tag or manual → installer → optional sign → GitHub Release).*

详见 / See also: [`docs/CI_CD.md`](docs/CI_CD.md)、[`docs/FEATURES.md`](docs/FEATURES.md)。

---

## 许可证 / License

Prismica 以 **MIT 许可证**发布。详见 [`LICENSE`](LICENSE)。
Prismica is released under the **MIT License**. See [`LICENSE`](LICENSE).
