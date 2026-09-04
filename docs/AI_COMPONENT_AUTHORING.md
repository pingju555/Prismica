# Prismica 组件创作指南（面向 AI Agent）

> 用途：本文档是 Prismica 桌面组件 `.pri` 格式的**权威参考 + 可直接丢给任意 AI Agent 的提示词模板**。
> 你（用户）无需手写组件——把本文档 + 你的需求交给你的 AI（GPT / Claude / 本地模型等），让它帮你生成或编辑 `.pri` 文件即可。
> 配套可运行示例见同目录 `examples/clock-cpu-theme.pri`。

---

## 0. 怎么用这份文档

三种典型用法，复制对应模板，把需求填进去，发给你的 AI：

| 你想做的事 | 用哪个模板 |
|---|---|
| 从零做一个新组件 | [§8.1 创建模板](#81-模板一从零创建组件) |
| 修改 / 扩展已有 `.pri` | [§8.2 修改模板](#82-模板二修改现有组件) |
| 你的 `.pri` 出错让 AI 诊断 | [§8.3 诊断模板](#83-模板三诊断错误) |

AI 交付的 `.pri` 文本，按 [§7 放置与加载](#7-放置与加载) 丢进 `Components/` 目录即可。

---

## 1. `.pri` 文件是什么

- **INI 风格**的纯文本声明式文件，一个文件 = 一个可独立渲染的桌面组件（widget）。
- 解析器：`Prismica.Core.Parsing.IniSkinTextParser`。
- 大小写**不敏感**（段名、键名都忽略大小写），但**度量/组件的内部名保留原始大小写**。
- 颜色用十六进制：`#RRGGBB` 或 `#AARRGGBB`（`AA` 缺省补 `FF`）。

### 1.1 总体结构

```ini
[Prismica]        ; 必填：组件元信息
[Variables]       ; 可选：颜色变量 #Var#
[Interface.*]     ; 可选：暴露给 Studio 参数编辑器的可编辑项
[Measure*]        ; 可选：数据源（时间 / CPU / 内存 / 计算）
[Meter*]          ; 可选：视觉元素（文本 / 进度条 / 容器）
[Style*]          ; 可选：样式段（解析层支持，渲染层待完善）
[Animation*]      ; 可选：动画
[Theme.*]         ; 可选：主题调色板
```

### 1.2 语法规则（违反会报警告/错误，见 §6）

- 段：`[段名]`，大小写不敏感。
- 注释：`;` 或 `#` 开头的整行。
- key-value：`Key=Value`，缺 `=` 是**错误**。`Key` 与 `Value` 两端空白会被裁剪。
- 段外的 key-value → 警告 `ORPHAN_KEY`。
- 同段重复 key → 警告 `DUPLICATE_KEY`（后者被忽略）。
- **缺少 `[Prismica]` 段 → 错误 `NO_PRISMICA_SECTION`**，文件视为非法。

---

## 2. `[Prismica]` 段（必填）

| 键 | 含义 | 默认 |
|---|---|---|
| `Version` | 组件版本号 | `0.1` |
| `Name` | **组件名**（不是文件名！用于 desktop.profile 引用） | `Unnamed` |
| `Author` | 作者 | 空 |
| `Description` | 描述 | 空 |
| `MeasureGrid` | 度量栅格 | `40` |
| `Update` | 刷新间隔（毫秒） | `1000` |
| `Width` | 组件默认宽（px） | `200` |
| `Height` | 组件默认高（px） | `120` |
| `Theme` | 活动主题名（对应某个 `[Theme.*]`） | 无 |

```ini
[Prismica]
Version=0.1
Name=ClockCpu
Author=YourName
Description=时钟 + CPU
Update=1000
Width=260
Height=96
Theme=Dark
```

---

## 3. `[Variables]` 段：颜色变量

- 每个 key 是一个颜色变量，value 为十六进制色。
- 在 Meter 文本 / 字段中用 `#VarName#` 引用。
- value 可本身就是**主题令牌** `@Theme.Key`（解析时先被主题替换，再作变量值）。

```ini
[Variables]
TitleColor=#FFF3F3F3
Accent=@Theme.Accent      ; 引用主题令牌，活动主题切换时一起变
```

使用：Meter 里 `FontColor=#TitleColor#`（或在字段里直接写 `@Theme.Accent`，见 §5）。

---

## 4. `[Interface.*]` 段：可编辑参数（给 Studio 用）

> ⚠️ **重要限制**：`[Interface.*]` 目前只是**声明元数据**，会被 Studio 参数编辑器读取，供用户在 UI 里改；但**运行时并不会把 `@Interface.X` 注入到 Meter**（此替换尚未实现）。
> 因此：用 `[Variables]` + `@Theme.X` 来做"会变的颜色/文本"，不要用 `@Interface.X` 当渲染令牌。

| 键 | 含义 |
|---|---|
| `Type` | `Text` / `Number` / `Color` / `Font` / `Bool` / `Select` / `Slider` / `Url` |
| `Default` | 默认值 |
| `Label` | 编辑器中显示的中文标签 |
| `Min` / `Max` / `Step` | 数值范围（Number/Slider 用） |
| `Options` | 逗号分隔的候选项（Select 用） |
| `ApplyTo` | 预留（当前未消费） |

```ini
[Interface.Title]
Type=Text
Default=我的电脑
Label=标题文本

[Interface.Accent]
Type=Color
Default=#FF4C8BF5
Label=强调色
```

---

## 5. `[Measure*]` 段：数据源

- 段名可任意，但**必须以 `Measure` 开头**（大小写不敏感）。
- 段内的 `Measure=` 选择类型。
- **度量名 = 完整段名**（含 `Measure` 前缀），例如 `[MeasureCpu]` 的度量名是 `MeasureCpu`。
- 其它地方（公式、`MeasureName=`）引用度量时，**必须用这个完整名**。

### 类型一览

| `Measure=` | 说明 | 关键字段 |
|---|---|---|
| `Time` | 系统时间/日期 | `Format`（strftime 风格，如 `%H:%M:%S`、`%Y-%m-%d`）、`TimeZone`（`Local`/具体） |
| `CPU` | CPU 占用率（**当前为简化实现，返回随机数**） | `Processor`（`0`=总计）、`Logical`（`true`/`false`） |
| `Memory` | 内存占用（简化，返回 GC 占用 MB） | `Type`（`Physical`/`Virtual`/`PageFile`） |
| `Calc` | 公式运算，引用其它度量 | `Formula`（见 §6）、`UpdateDivider` |

```ini
[MeasureTime]
Measure=Time
Format=%H:%M:%S

[MeasureCpu]
Measure=CPU

[MeasureCpuPct]
Measure=Calc
Formula=[MeasureCpu]
```

---

## 6. 公式语言（仅用于 `Calc` 度量的 `Formula=`）

- 引用其它度量：`[度量全名]`，例如 `[MeasureCpu]`。
- 运算符：`+ - * / % ^`、比较 `== != < > <= >=`、逻辑 `and or`。
- 三元：`if(cond, a, b)` 或 `iif(cond, a, b)`。
- **只认度量，不认 Meter**（Meter 不会进入公式上下文）。

### 内建函数表

**数学**：`abs` `ceil` `floor` `round` `sqrt` `min(a,b,...)` `max(a,b,...)` `clamp(value,min,max)` `lerp(a,b,t)` `sin` `cos` `tan` `asin` `acos` `atan` `atan2(y,x)` `rad(deg)` `deg(rad)` `log` `log10` `exp` `pow(base,exp)`

**字符串**：`substr(s,start,len?)` `strlen(s)` `upper(s)` `lower(s)` `trim(s)` `replace(s,old,new)` `contains(s,sub)` `startswith(s,sub)` `endswith(s,sub)`

**条件/时间**：`if(cond,a,b)` `iif(cond,a,b)` `time(fmt?)` `timestamp()`

```ini
[MeasureHalf]
Measure=Calc
Formula=clamp([MeasureCpu], 0, 100) / 2

[MeasureGreeting]
Measure=Calc
Formula=if([MeasureCpu] > 50, 1, 0)
```

---

## 7. `[Meter*]` 段：视觉元素

- 段名任意，以 `Meter` 开头；段内 `Meter=` 选类型。
- **Meter 名 = 去掉 `Meter` 前缀后的部分**：`[MeterClock]` → 名 `Clock`（动画 `Target` 用这个名字）。
- Meter 通过 `MeasureName=度量全名` 绑定数据源；或文本里用 `[度量全名]` 简写取数。
- 通用布局字段：`X` `Y` `W` `H`（像素，可写在同一行 `X=0 Y=0 W=260 H=46`）。

### 7.1 `String`（文本）

| 字段 | 说明 | 默认 |
|---|---|---|
| `Text` | 文本内容；可含 `#Var#`、公式不在此支持、`[度量全名]` 简写取数 | 空 |
| `MeasureName` | 绑定的度量全名 | 无 |
| `FontFace` | 字体 | `Segoe UI` |
| `FontSize` | 字号 | `14` |
| `FontColor` | 字体颜色 | 白 |
| `FontWeight` | `Normal`/`Bold` | `Normal` |
| `StringAlign` | `Left`/`Center`/`Right` | `Left` |
| `ClipString` | 过长截断 | `false` |

```ini
[MeterClock]
Meter=String
MeasureName=MeasureTime
X=0 Y=0 W=260 H=46
FontSize=36
FontColor=@Theme.Text        ; 直接引用主题令牌
StringAlign=Center

[MeterNote]
Meter=String
Text=你好 #TitleColor#        ; 也支持 #变量#（此处仅作文本字面量示例）
X=0 Y=50 W=260 H=20
FontSize=13
```

> 颜色字段（`FontColor`、`BarColor` 等）都**可以直接写 `@Theme.Key`** 实现一键换肤，无需经 `[Variables]`。

### 7.2 `Progress`（进度条）

| 字段 | 说明 | 默认 |
|---|---|---|
| `MeasureName` | 绑定的度量全名（取 0–100 数值） | 无 |
| `BarColor` | 进度条颜色 | `#00FF88` |
| `BackgroundColor` | 背景色 | `#40000000` |
| `BorderColor` | 边框色 | 透明 |
| `BorderWidth` | 边框宽 | `1` |
| `Radius` | 圆角半径 | `0` |
| `Orientation` | `Horizontal`/`Vertical`/`Radial` | `Horizontal` |
| `Invert` | 反向填充 | `false` |
| `Animation` | 过渡动画 | `true` |

### 7.3 `Container`（容器）

| 字段 | 说明 | 默认 |
|---|---|---|
| `ClipToBounds` | 裁剪子元素 | `true` |
| `Layout` | `Canvas`/`Stack`/`Grid` | `Canvas` |

> 注：容器当前仅解析层支持，嵌套子元素渲染待完善。

---

## 8. `[Animation*]` 段：动画

- 段名任意，以 `Animation` 开头；无后缀表示默认动画。
- 字段：

| 字段 | 说明 | 默认 |
|---|---|---|
| `Trigger` | `OnShow` / `OnHide` / `OnUpdate` / `OnClick` / `Manual` | `OnShow` |
| `Target` | 目标 Meter 名（**去掉 `Meter` 前缀**，如 `MeterClock`→`Clock`） | 空 |
| `Property` | `Opacity` / `X` / `Y` / `ScaleX` / `ScaleY` / `Rotation` | `Opacity` |
| `From` / `To` | 起止值 | `0` / `1` |
| `Duration` | 时长（毫秒） | `300` |
| `Easing` | 缓动名（见下表） | `Linear` |
| `AutoReverse` | 结束后反向播回 | `False` |
| `Repeat` | `0`=一次，`-1`=无限，`N`=重复 N 次 | `0` |
| `Delay` | 延迟（毫秒） | `0` |

**缓动名（大小写不敏感）**：`Linear`、`EaseInQuad`、`EaseOutQuad`、`EaseInOutQuad`、`EaseInCubic`、`EaseOutCubic`、`EaseInOutCubic`、`EaseInQuart`、`EaseOutQuart`、`EaseInOutQuart`、`EaseInExpo`、`EaseOutExpo`、`EaseInOutExpo`、`EaseInSine`、`EaseOutSine`、`EaseInOutSine`、`EaseInCirc`、`EaseOutCirc`、`EaseInOutCirc`、`EaseInBack`、`EaseOutBack`、`EaseInOutBack`、`EaseInElastic`、`EaseOutElastic`、`EaseInOutElastic`、`EaseInBounce`、`EaseOutBounce`、`EaseInOutBounce`。

```ini
[AnimationFadeIn]
Trigger=OnShow
Target=Clock
Property=Opacity
From=0
To=1
Duration=500
Easing=EaseOutQuad
```

---

## 9. `[Theme.*]` 段 + `[Prismica] Theme=`：主题换肤

- `[Theme.名字]` 定义一个调色板，里面是 `Key=Value` 令牌（通常放颜色）。
- `[Prismica] Theme=名字` 选择活动主题。
- 其它段里用 `@Theme.Key` 引用令牌；**主题段自身不被替换**。
- 切换活动主题 → 所有 `@Theme.Key` 一次性换色（Desktop 右键"切换主题"即触发）。

```ini
[Prismica]
Theme=Dark
; ...

[Theme.Dark]
Text=#FFF3F3F3
Sub=#FF9A9A9A
Accent=#FF4C8BF5

[Theme.Light]
Text=#FF1A1A1A
Sub=#FF666666
Accent=#FF1976D2
```

---

## 10. `desktop.profile`：多屏差异化（独立文件）

> 这是**另一个文件**，不在 `.pri` 里。决定每个屏幕加载哪些组件。

- 加载顺序：`%LOCALAPPDATA%\Prismica\desktop.profile` → 否则 `<程序目录>/Components/desktop.profile` → 否则内置默认（`Default=ClockCpu`）。
- 格式：

```ini
[Desktop]
Version=0.1
Default=ClockCpu                ; 未匹配屏幕时的回退组件

[Screen.Primary]
Components=ClockCpu,Weather

[Screen.Secondary]
Components=IconGrid

[Screen.1]                      ; 也可用数字索引或设备名子串匹配
Components=ClockCpu
```

- 段键匹配优先级：`Primary`（主屏）→ `Secondary`（首个非主屏）→ 数字索引 → 设备名子串。
- `Components=` 列出的是**组件名**（= `.pri` 的 `[Prismica] Name`），不是文件名，逗号或分号分隔。

---

## 11. 放置与加载

1. 把 `.pri` 放到 `<程序目录>/Components/你的组件.pri`（Studio 与 Desktop 都扫描这个目录）。
2. **Studio**：左侧组件库双击即可加载并实时预览；右侧 Tab 可编辑参数 / 公式 / 动画 / 主题 / 多屏。
3. **Desktop**：在 `desktop.profile` 的 `Default=` 或对应 `[Screen.*]` 的 `Components=` 里写上组件名（= `[Prismica] Name`）。改 `.pri` 后程序会热重载只引用该文件的窗口。
4. 组件名冲突时以 `[Prismica] Name` 为准，建议每个组件 Name 唯一。

---

## 12. 校验与常见错误

解析会产生诊断（Diagnostic），严重的是 Error、其余是 Warning：

| 诊断码 | 含义 | 处理 |
|---|---|---|
| `NO_PRISMICA_SECTION` | 缺 `[Prismica]` | 顶部补上 |
| `INVALID_KV` | 行内缺 `=` | 改成 `Key=Value` |
| `ORPHAN_KEY` | 段外的 key-value | 移到某段内 |
| `DUPLICATE_KEY` | 同段重复 key | 删掉重复 |
| `INVALID_COLOR` | 颜色格式错 | 用 6/8 位 `#RRGGBB`/`#AARRGGBB` |
| `THEME_DUP` | 重复主题名 | 改名 |
| `THEME_ACTIVE_MISSING` | `Theme=` 指向未定义主题 | 补 `[Theme.X]` 或改名字 |
| `THEME_UNKNOWN_TOKEN` | 引用了未定义令牌 `@Theme.X` | 在活动主题里加该 Key |
| `SCREEN_DUP` | 重复屏幕键 | 改名 |
| `SCREEN_EMPTY` | 某屏没给组件 | 补 `Components=` |
| `SCREEN_NO_DEFAULT` | 没设默认组件 | 补 `Default=` |
| `SCREEN_UNKNOWN_COMPONENT` | 引用了不存在的组件名 | 确认 `[Prismica] Name` 拼写 |
| `SCREEN_UNASSIGNED` | 屏幕键没匹配到实际屏幕 | 用 `Primary`/`Secondary`/数字 |

**最常踩的坑**
1. **Meter 名不能用于公式 / `MeasureName`**：只有 Measure 进公式上下文。要引用数据，先建 `[MeasureXxx]` 再用 `[MeasureXxx]`。
2. **引用度量必须带 `Measure` 前缀**：写 `[MeasureCpu]` 不是 `[Cpu]`，`MeasureName=MeasureCpu` 不是 `MeasureName=Cpu`。
3. **动画 `Target` 是 Meter 名（去前缀）**：`[MeterClock]` → `Target=Clock`。
4. **`@Interface.X` 不生效**：运行时不会注入，用 `[Variables]`/`@Theme.X`。
5. **组件名 ≠ 文件名**：desktop.profile 引用的是 `[Prismica] Name`。

---

## 13. 已知限制（写作时）

- `Embed` 段仅基础设施，**无内建类型**（图标格子 embed 尚未接通），写了也不会渲染。
- `CPU` / `Memory` 度量为**简化实现**（随机 / GC 估算），非真实性能计数。
- `[Style*]` / 嵌套 Container 子元素：解析支持，渲染层待完善。
- `[Interface.*]` 仅 Studio 编辑元数据，运行时无令牌注入。
- 公式为声明式运算，不支持在 `String` 的 `Text` 里直接写算术（请用 `Calc` 度量 + `MeasureName` 绑定）。

---

## 14. 给你的 AI Agent 的提示词模板

### 8.1 模板一：从零创建组件

> 你是一个 Prismica 桌面组件（`.pri`）生成器。请严格按下面这份格式规范输出**单个完整 `.pri` 文件**。
>
> 规范要点：
> 1. 必须有 `[Prismica]` 段，含 `Version/Name/Author/Description/Update/Width/Height/Theme`。`Name` 是组件名（不是文件名）。
> 2. 数据源写 `[MeasureXxx]`（`Measure=Time|CPU|Memory|Calc`）；公式用 `Formula=[MeasureOther]` 引用其它度量全名。
> 3. 视觉写 `[MeterXxx]`（`Meter=String|Progress|Container`），用 `MeasureName=MeasureXxx` 绑定数据；布局用 `X/Y/W/H`。
> 4. 颜色用 `#RRGGBB` 或 `#AARRGGBB`；需要换肤就定义 `[Theme.Dark]/[Theme.Light]` 并让字段写 `@Theme.Key`，`[Prismica] Theme=` 选活动主题。
> 5. 动画写 `[AnimationXxx]`，`Target` 用 Meter 名（去 `Meter` 前缀），`Easing` 用 Linear/EaseOutQuad 等。
> 6. 不要使用 `@Interface.X` 做渲染令牌（尚未实现）。
> 7. 只输出 `.pri` 文本内容，不要加解释。
>
> 我的需求：{在此描述你要的组件，例如"一个 260x96 的时钟，下面带 CPU 占用进度条，支持明暗主题"}

### 8.2 模板二：修改现有组件

> 这是我的 Prismica 组件 `.pri` 文件（原样粘贴）：
> ```
> {在此粘贴现有 .pri 全文}
> ```
> 请基于上面的格式规范，做如下修改：{例如"把进度条颜色改成主题令牌 @Theme.Accent，并加一个 OnShow 淡入动画"}。
> 只输出修改后的完整 `.pri`，不要省略任何未改动的部分。

### 8.3 模板三：诊断错误

> 我的 Prismica 组件 `.pri` 如下：
> ```
> {在此粘贴 .pri}
> ```
> 解析报错（粘贴错误信息或描述现象）：{例如"组件不显示 / 进度条为空 / 报 NO_PRISMICA_SECTION"}。
> 请对照规范（常见坑：Meter 名不能用于公式、引用度量必须带 Measure 前缀、动画 Target 是去前缀的 Meter 名、组件名≠文件名）定位问题并给出修复后的完整 `.pri`。

---

## 15. 推荐工作流

1. 用 §8.1 模板让 AI 生成初版 → 存为 `Components/MyWidget.pri`。
2. 用 Studio 打开预览，肉眼检查布局/颜色。
3. 用 §8.2 模板迭代修改（"把 X 调到 Y"、"加一个主题"）。
4. 出错用 §8.3 模板诊断。
5. 多屏用户再补 `desktop.profile`（§10）。

完整可运行范例：`examples/clock-cpu-theme.pri`（时钟 + CPU 进度 + 明暗主题 + 淡入动画）。
