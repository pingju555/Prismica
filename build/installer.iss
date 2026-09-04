; ============================================================
; Prismica installer script (Inno Setup 6+)
; ------------------------------------------------------------
; Invoked automatically by build/Publish.ps1, or manually:
;   iscc /dMyAppVersion=0.1.0-alpha /dMyPublishRoot="<abs path>\dist\publish" build\installer.iss
;
; Placeholders (injected via /d; fall back to dev defaults below if absent)
; ============================================================
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0-alpha"
#endif
#ifndef MyPublishRoot
  #define MyPublishRoot "..\dist\publish"
#endif

#define MyAppName "Prismica"
#define MyAppPublisher "Prismica Team"
#define MyAppURL "https://github.com/prismica/prismica"
; Windows version info only accepts numeric dotted versions; strip -alpha/-beta/-rc suffixes
#define VerCore StringChange(MyAppVersion, "-alpha", "")
#define VerCore StringChange(VerCore, "-beta", "")
#define VerCore StringChange(VerCore, "-rc", "")

[Setup]
; Unique install instance id - do not change casually (would be treated as a fresh install)
AppId={{A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#VerCore}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Desktop Widget Engine
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\dist
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Matches the project TFM (net8.0-windows10.0.19041.0)
MinVersion=10.0.19041
SetupLogging=yes
ChangesEnvironment=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
; Desktop host (single-file self-contained exe + appsettings.json etc.)
Source: "{#MyPublishRoot}\Desktop\*"; DestDir: "{app}\Desktop"; Flags: ignoreversion recursesubdirs createallsubdirs
; Studio editor
Source: "{#MyPublishRoot}\Studio\*"; DestDir: "{app}\Studio"; Flags: ignoreversion recursesubdirs createallsubdirs
; Seeded example component (default profile Default=ClockCpu -> works out of the box)
Source: "{#MyPublishRoot}\Components\*"; DestDir: "{app}\Components"; Flags: ignoreversion recursesubdirs createallsubdirs
; Offline authoring guide
Source: "{#MyPublishRoot}\Docs\AI_COMPONENT_AUTHORING.md"; DestDir: "{app}\Docs"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName} Desktop"; Filename: "{app}\Desktop\Prismica.Desktop.exe"; WorkingDir: "{app}\Desktop"
Name: "{group}\{#MyAppName} Studio"; Filename: "{app}\Studio\Prismica.Studio.exe"; WorkingDir: "{app}\Studio"
Name: "{group}\Component Authoring Guide"; Filename: "{app}\Docs\AI_COMPONENT_AUTHORING.md"
Name: "{autodesktop}\{#MyAppName} Desktop"; Filename: "{app}\Desktop\Prismica.Desktop.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional tasks:"; Flags: unchecked
Name: "startup"; Description: "Launch {#MyAppName} Desktop at Windows startup"; GroupDescription: "Additional tasks:"

[Run]
Filename: "{app}\Desktop\Prismica.Desktop.exe"; Description: "Launch {#MyAppName} Desktop now"; Flags: nowait postinstall skipifsilent

[Registry]
; Run at startup: written only when the user selects the startup task (auto-cleaned on uninstall)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PrismicaDesktop"; ValueData: "{app}\Desktop\Prismica.Desktop.exe"; Flags: uninsdeletevalue; Tasks: startup

[UninstallDelete]
; User data (custom components, AppData config) is preserved by default. Uncomment to also wipe it:
; Type: filesandordirs; Name: "{userappdata}\Prismica"
