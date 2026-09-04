using System;
using System.IO;
using System.Reflection;

namespace Prismica.App;

/// <summary>
/// 开机自启管理器：通过 Startup 文件夹快捷方式实现。
/// </summary>
public static class AutoStartManager
{
    private static string StartupFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "Startup");

    private static string ShortcutName => "Prismica.lnk";

    private static string ShortcutPath => Path.Combine(StartupFolder, ShortcutName);

    public static bool IsAutoStartEnabled => File.Exists(ShortcutPath);

    public static void Enable()
    {
        if (IsAutoStartEnabled) return;

        var exePath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrEmpty(exePath)) return;

        // 创建快捷方式（简单 VBS 脚本方式）
        var vbsContent = $@"
Set oWS = WScript.CreateObject(""WScript.Shell"")
sLinkFile = ""{ShortcutPath}""
Set oLink = oWS.CreateShortcut(sLinkFile)
oLink.TargetPath = ""{exePath}""
oLink.WorkingDirectory = Path.GetDirectoryName(exePath)
oLink.Save
";

        var vbsPath = Path.Combine(Path.GetTempPath(), "prismica_autostart.vbs");
        File.WriteAllText(vbsPath, vbsContent);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cscript", $"//nologo \"{vbsPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        finally
        {
            try { File.Delete(vbsPath); } catch { }
        }
    }

    public static void Disable()
    {
        if (!IsAutoStartEnabled) return;
        try { File.Delete(ShortcutPath); } catch { }
    }

    public static void Toggle()
    {
        if (IsAutoStartEnabled) Disable();
        else Enable();
    }
}
