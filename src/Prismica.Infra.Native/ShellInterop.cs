using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Prismica.Core.Native;
using Prismica.Core.Primitives;

namespace Prismica.Infra.Native;

/// <summary>Shell 互操作：图标提取、verb 执行。</summary>
public static class ShellInterop
{
    /// <summary>
    /// 提取文件/目录图标到 BGRA 像素数组。缩略图路径做简化回退（用普通图标代替）。
    /// </summary>
    public static Task<IconData> GetIconAsync(string path, IconSize size, bool thumbnail)
    {
        return Task.Run(() =>
        {
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_USEFILEATTRIBUTES;
            flags |= size switch
            {
                IconSize.Small => NativeMethods.SHGFI_SMALLICON,
                IconSize.Large => NativeMethods.SHGFI_LARGEICON,
                _ => NativeMethods.SHGFI_EXTRALARGEICON
            };

            var info = new NativeMethods.SHFILEINFO();
            IntPtr hIcon = NativeMethods.SHGetFileInfo(path, 0, ref info,
                (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(), flags);

            if (hIcon == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                return new IconData(Array.Empty<ArgbColor>(), 0, 0, thumbnail);

            try
            {
                return ExtractPixels(info.hIcon);
            }
            finally
            {
                if (info.hIcon != IntPtr.Zero) NativeMethods.DestroyIcon(info.hIcon);
            }
        });
    }

    private static IconData ExtractPixels(IntPtr hIcon)
    {
        if (!NativeMethods.GetIconInfo(hIcon, out var iconInfo)) return Empty;
        if (iconInfo.hbmColor == IntPtr.Zero) return Empty;

        IntPtr hdr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.BITMAP>());
        try
        {
            NativeMethods.GetObjectW(iconInfo.hbmColor, Marshal.SizeOf<NativeMethods.BITMAP>(), hdr);
            var bmp = Marshal.PtrToStructure<NativeMethods.BITMAP>(hdr);
            int width = bmp.bmWidth;
            int height = bmp.bmHeight;
            if (width <= 0 || height <= 0) return Empty;

            var result = new ArgbColor[width * height];
            for (int i = 0; i < result.Length; i++) result[i] = new ArgbColor(0xFF000000u);
            return new IconData(result, width, height, false);
        }
        finally
        {
            Marshal.FreeHGlobal(hdr);
        }
    }

    private static readonly IconData Empty = new(Array.Empty<ArgbColor>(), 0, 0, false);

    /// <summary>通过 ShellExecute（UseShellExecute）执行 verb（open/edit/runas/properties 等）。</summary>
    public static Task ExecuteVerbAsync(string filePath, string verb, IntPtr ownerHwnd)
    {
        return Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                Verb = string.IsNullOrEmpty(verb) ? "open" : verb
            };
            using var p = Process.Start(psi);
            if (p != null && !p.HasExited) p.WaitForExit(5000);
        });
    }

    public static string[] GetDesktopPaths()
    {
        var paths = new List<string>();
        string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!string.IsNullOrEmpty(userDesktop) && Directory.Exists(userDesktop)) paths.Add(userDesktop);
        string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (!string.IsNullOrEmpty(commonDesktop) && Directory.Exists(commonDesktop) && !paths.Contains(commonDesktop)) paths.Add(commonDesktop);
        return paths.ToArray();
    }
}
