[Prismica]
Version=0.1
Name=Weather
Author=Prismica
Description=Weather widget with current temperature and condition
MeasureGrid=1
Update=600000
Width=200
Height=100

[Interface.City]
Type=Text
Default=Beijing
Label=City Name

[Interface.Unit]
Type=Text
Default=C
Label=Temperature Unit (C/F)

[EmbedWeather]
Type=Embed
Keyword=Weather
X=0
Y=0
W=200
H=100

[MeterBackground]
Type=Shape
MeasureName=
X=0
Y=0
W=200
H=100
FillColor=30,58,95,255
Corner=8
