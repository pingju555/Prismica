; Prismica G3 示例组件：时钟 + CPU 占用
[Prismica]
Version=0.1
Name=ClockCpu
Author=prismica
Description=G3 示例：实时时钟 + CPU 进度条
Update=1000
Width=240
Height=140

[Variables]
FontColor=#FFFFFFFF
BarColor=#FF00FF88

[MeasureClock]
Measure=Time
Format=%H:%M:%S

[MeasureCpu]
Measure=CPU

[MeterClock]
Meter=String
MeasureName=MeasureClock
X=0
Y=0
W=240
H=44
FontFace=Segoe UI
FontSize=36
FontColor=#FontColor#

[MeterCpuLabel]
Meter=String
Text=CPU LOAD
X=70
Y=54
W=100
H=22
FontSize=16
FontColor=#FontColor#

[MeterCpuBar]
Meter=Progress
MeasureName=MeasureCpu
X=70
Y=82
W=100
H=14
BarColor=#BarColor#
BackgroundColor=#40000000

[EmbedClock]
Embed=Clock
X=0
Y=100
W=240
H=36
