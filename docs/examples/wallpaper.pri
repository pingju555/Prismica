; ============================================================
; 示例动态壁纸组件（路线 B 壁纸层）
; 教程见 docs/AI_COMPONENT_AUTHORING.md
; 用法：把本文件复制为 <程序目录>/Components/wallpaper.pri
;       （文件名必须是 wallpaper.pri，组件名 Wallpaper，与默认壁纸组件名一致）
; 行为：整窗透明，只有底部/角落的时钟与 CPU 条是"内容"；
;       透明区域点击会穿透到桌面（图标/窗口），命中内容才被壁纸接收。
;
; 媒体壁纸（GIF/MP4，整窗点击穿透、不预计算遮罩）改用桌面配置，而非本 .pri：
;   Prismica:Desktop:Wallpaper:Mode=Image
;   Prismica:Desktop:Wallpaper:ImagePath=<绝对路径>/bg.gif        ; 或 .mp4 / .webm / .avi
; （PNG 仍走逐像素 alpha 遮罩穿透；GIF 逐帧动画、视频全屏循环播放，二者均整窗穿透。）
; ============================================================

[Prismica]
Version=0.1
Name=Wallpaper
Author=Example
Description=动态壁纸示例：透明背景 + 角落时钟/CPU
Update=1000
Width=320
Height=120
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

; 角落时钟（内容区——命中此处点击不穿透）
[MeterClock]
Meter=String
MeasureName=MeasureTime
X=0 Y=0 W=320 H=46
FontSize=34
FontColor=@Theme.Text
FontFace=Segoe UI
StringAlign=Center
Shadow=1

[MeterLabel]
Meter=String
Text=SYSTEM LOAD
X=0 Y=48 W=320 H=18
FontSize=12
FontColor=@Theme.Sub
StringAlign=Center

[MeterCpuBar]
Meter=Progress
MeasureName=MeasureCpuPct
X=60 Y=72 W=200 H=12
BarColor=@Theme.Accent
BackgroundColor=#40000000
Orientation=Horizontal

[AnimationIntro]
Trigger=OnShow
Target=Clock
Property=Opacity
From=0
To=1
Duration=600
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
