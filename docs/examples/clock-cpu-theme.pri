; ============================================================
; 示例组件：时钟 + CPU 占用进度条，支持明暗主题一键切换
; 教程见 docs/AI_COMPONENT_AUTHORING.md
; 把本文件放到 <程序目录>/Components/ 下即可被 Studio / Desktop 加载
; 注意：[Prismica] Name 是组件名（不是文件名），用于 desktop.profile 引用
; ============================================================

[Prismica]
Version=0.1
Name=ClockCpu
Author=Example
Description=时钟 + CPU 占用，明暗主题一键切换
Update=1000
Width=260
Height=96
Theme=Dark

[Variables]
TitleColor=@Theme.Text
SubColor=@Theme.Sub
Accent=@Theme.Accent

[MeasureTime]
Measure=Time
Format=%H:%M:%S

[MeasureCpu]
Measure=CPU

[MeasureCpuPct]
Measure=Calc
Formula=[MeasureCpu]

[MeterClock]
Meter=String
MeasureName=MeasureTime
X=0 Y=0 W=260 H=46
FontSize=36
FontColor=@Theme.Text
FontFace=Segoe UI
StringAlign=Center

[MeterLabel]
Meter=String
Text=系统负载
X=0 Y=48 W=260 H=20
FontSize=13
FontColor=@Theme.Sub
StringAlign=Center

[MeterCpuBar]
Meter=Progress
MeasureName=MeasureCpuPct
X=30 Y=74 W=200 H=12
BarColor=@Theme.Accent
BackgroundColor=#40000000
Orientation=Horizontal

[AnimationIntro]
Trigger=OnShow
Target=Clock
Property=Opacity
From=0
To=1
Duration=500
Easing=EaseOutQuad
AutoReverse=False
Repeat=0
Delay=0

[Theme.Dark]
Text=#FFF3F3F3
Sub=#FF9A9A9A
Accent=#FF4C8BF5

[Theme.Light]
Text=#FF1A1A1A
Sub=#FF666666
Accent=#FF1976D2
