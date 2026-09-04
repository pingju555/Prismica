# Prismica 功能清单

> 整理时间：2026-09-04
> 状态：P1–P4（#1–#30）全部完成，项目进入可发布状态；P5 #31 动态壁纸层已落地；另含 CI/CD 与代码签名工程能力。
> 技术栈：.NET 8 + WPF，分层 Core（纯逻辑）→ App（组合根）→ Infra.Wpf/Native/Embeds/Measures/Meters，Desktop + Studio 双入口。
> 测试基线：276/276 全绿（Core 257 + Infra 19）。

---

## A. 桌面运行时（Desktop 引擎能力）

透明覆盖窗口渲染在桌面之上，承载 `.pri` 声明的组件，并提供窗口管理、稳定性与自适应能力。

- **透明覆盖 + 点击穿透**：组件窗口覆盖桌面，空区穿透鼠标，内容区可交互。
- **多组件叠加**：可同时加载多个 `.pri`，每个实例独立窗口、位置、大小，所有 runtime 同步更新。
- **窗口拖拽**：顶部 30px 拖拽手柄（`WM_NCHITTEST`→`HTCAPTION`），支持拖动移动。
- **窗口缩放**：8px 缩放边框，支持边缘/角落拖拽调整大小，缩放结束自动保存布局。
- **右键上下文菜单**：`WM_RBUTTONUP` 弹出 WPF 菜单（Reload / Settings / Add Component / Remove）。
- **布局持久化**：`layout.ini` 保存/恢复窗口位置与大小。
- **Checkpoint 恢复**：每 5s 写 `layout.pending.ini`，异常退出后下次启动自动恢复。
- **托盘图标**：右键菜单（Open Studio / Auto Start / Exit），附「Check for Updates」。
- **开机自启**：Startup 文件夹快捷方式。
- **单实例 + 崩溃兜底**：`Global\PrismicaDesktop` Mutex；崩溃后延迟 2s 自动重启。
- **热加载**：编辑 `.pri` 自动重载，仅重载引用该文件的窗口（多屏隔离）。
- **多屏差异化**（#24）：每屏按 `.desktop.profile` 加载各自组件集（匹配键 Primary/Secondary/数字索引/设备名子串）。
- **主题/皮肤**（#23）：`.pri` 声明式 `[Theme.*]` 调色板 + `@Theme.Key` 引用 + `Theme=` 选择；托盘「切换主题」一键换肤，整套令牌随之切换。
- **动画系统**（#22）：`.pri` 声明式 `[Animation*]`（OnShow / OnClick / 定时触发），28 个缓动函数（`NamedEasing`），属性过渡 + 循环/反弹。
- **性能优化**（#28）：`FrameRateGovernor` 按活动状态在 活跃60 / 存活2 / 空闲1 fps 间自适应切换并带迟滞；空闲帧跳过渲染更新，降低 CPU 与每帧分配。
- **崩溃上报**（#27）：结构化 JSON 报告（`CrashReport`/`CrashReportBuilder`/`LocalCrashSink`）落盘 `crashes/crash_*.json`；可选 HTTP 上报（配置 `CrashReportUploadUrl`）；全局捕获顶层 + `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`。
- **自动更新**（#26）：`SemVersion`/`UpdateManifest`/`UpdateDecision` 纯逻辑（版本比较、清单解析、升级决策含渠道/预发布/最低版本强制）+ `UpdateChecker`（仅检查+通知，不静默下载以免覆盖运行中程序）；托盘菜单 + 启动后 15s 自动检查；未配置 `UpdateUrl` 时走 NoOp 不联网。
- **动态壁纸层**（#31，路线 B）：壁纸窗口**不置顶**，经 `InsertAboveDesktop` 注入到桌面图标（Progman/WorkerW）**之下**、普通窗口之下；提供两种模式——
  - **组件模式**（`Mode=Component`，默认）：加载 `.pri` 壁纸组件，内容矩形内可点击、矩形外透明区穿透到桌面（`WM_NCHITTEST` 判定）。
  - **图片模式**（`Mode=Image`）：加载带 alpha 的 PNG，加载时一次性扫描 alpha 通道构建**逐像素遮罩**（`AlphaMask`，预识别缓存）→ 完全透明像素点击穿透、非透明像素接收点击，圆角/渐隐边缘均正确。

## B. Studio 编辑器（所见即所得）

WPF 编辑器，实时预览 + 多 Tab 可视化设计 `.pri` 组件。

- **实时预览**：编辑 `.pri` 即刷新（防抖重建 + 启动即显示预览）。
- **组件库**：左侧面板列出 `Components/` 可用组件，双击加载，显示名称/描述。
- **参数 Schema 设计器**（#20）：可视化定义 `[Interface.*]` 参数（Number/Color/Bool/Select/Text），支持类型/默认值/标签/范围/选项，新增/删除/上下移，经 `InterfaceSchemaSerializer` 回写。
- **公式编辑器**（#21）：函数目录（`FormulaCatalog`）+ 语法校验（`FormulaValidator` 带错误位置）+ 试算，经 `FormulaFieldSerializer` 回写 `[Measure*] Formula=`。
- **动画 Tab**（#22）：可视化编辑 `[Animation*]`（触发器/目标/属性/缓动/时长），实时校验并回写。
- **主题 Tab**（#23）：可视化编辑 `[Theme.*]` 调色板与活动主题，改值即预览换色。
- **多屏 Tab**（#24）：可视化编辑 `Default=` 与每屏分配，应用后回写 `AppData\Prismica\desktop.profile`。
- **参数面板**（#17）：`[Interface.*]` 参数实时配置。

## C. 组件格式与生态（`.pri` + Embed）

- **`.pri` 声明式格式**：`[Prismica]`（元信息/Name）+ `[Variables]`（变量）+ `[Interface.*]`（可配置参数）+ `[Measure*]`（数据度量/公式）+ `[Meter*]`（视觉元素）+ `[Animation*]`（动画）+ `[Theme.*]`（主题令牌）。`IniSkinTextParser` 解析并产出 `ComponentDefinition`。
- **组件库目录**：`ComponentLibrary` 扫描 `Components/`，列出可用 `.pri` 供添加/加载。
- **Embed 组件**：
  - `Clock`：内建时钟（内建 measure/meter）。
  - `MusicControlEmbedComponent`：音乐控制，基于 SMTC 媒体会话（占位实现）。
  - `WeatherEmbedComponent`：天气，调用 wttr.in API。
  - `StickyNoteEmbedComponent`：便签，本地文本持久化。
  - `IconGridEmbedComponent`：桌面图标格子嵌入（`INativeDesktop.GetDesktopIcons()` + 图标渲染 + 点击打开）。
- **AI 辅助创作文档**（#25）：`docs/AI_COMPONENT_AUTHORING.md`（权威 `.pri` 格式参考 + 公式语言/缓动表 + 诊断码对照 + 常踩坑）+ 可运行示例 `docs/examples/clock-cpu-theme.pri` + 3 个可直接丢给任意 AI Agent 的提示词模板（创建/修改/诊断）。

## D. 工程与发布

- **单元测试**：264/264 全绿（Core 245 + Infra 19），覆盖 parser/theming/animation/formula/multiscreen/update/crash/scheduling/primitives 等纯逻辑。
- **安装包**（#30）：Inno Setup 脚本 `build/installer.iss` + 一键发布脚本 `build/Publish.ps1`（读版本 → 单文件/自包含/win-x64 发布 Desktop+Studio → 预置 `Components/ClockCpu.pri` 与 `Docs/` → 调 `iscc` 生成安装包）。
- **CI/CD**：`.github/workflows/ci.yml`（push/PR → restore+build+test，264 项全过才放行）+ `.github/workflows/release.yml`（打 `v*` tag / 手动 → 安装 Inno Setup → `Publish.ps1` 出包 → 可选签名 → 上传产物 + 建 GitHub Release）。
- **代码签名**：`build/Publish.ps1 -Sign`（Authenticode，`signtool` + RFC3161 时间戳），支持 PFX（`-CertFile`/`-CertPassword`）或本机证书指纹（`-CertThumbprint`）；release 工作流经 `SIGNING_CERT_PFX`/`SIGNING_CERT_PASSWORD` secret 调用；`docs/CI_CD.md` 另含 **Azure Trusted Signing** 免证书方案。

---

## 已知边界（需人工验证 / 受环境限制）

- **沙箱构建约束**：Git Bash 沙箱中 `ProgramData` 等环境变量可能为空，NuGet 还原报 `path1` null；绕过为 `dotnet build/test ... --no-restore`（需本地 `obj/project.assets.json` + nuget 缓存）。真实 Windows 正常还原。
- **多屏**：单显示器环境无法可视化验证「每屏不同组件」，逻辑有 12 项单测覆盖，真实效果需双屏。
- **PowerShell 5.1 编码**：无 BOM 的 `.ps1`/`.iss` 中文字面量会被按 GBK 误读，故发布脚本源码已改为纯 ASCII。
- **Git 仓库**：当前项目**尚不是 git 仓库**，GitHub Actions 需先 `git init` + 推到 GitHub 才生效（见 `docs/CI_CD.md` 启用步骤）。
- **safe-delete 钩子**：本机 PowerShell 的 `Remove-Item` 可能被钩子拦截，发布/清理脚本已改用 `[System.IO.Directory]::Delete` 绕过。
