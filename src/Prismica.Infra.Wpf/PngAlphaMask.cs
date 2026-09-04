using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Prismica.Core.Wallpaper;

namespace Prismica.Infra.Wpf;

/// <summary>
/// 从 PNG 文件一次性扫描 alpha 通道，构建 <see cref="AlphaMask"/>（预识别缓存）。
/// 仅在加载/换图时调用一次；后续命中测试由 AlphaMask 提供 O(1) 查表。
/// </summary>
public static class PngAlphaMask
{
    /// <summary>
    /// 读取 PNG 的 alpha 通道，生成逐像素遮罩。
    /// </summary>
    /// <param name="path">PNG 文件路径（应含 alpha 通道，如 RGBA/ARGB）。</param>
    /// <param name="threshold">透明阈值，默认 0（仅 alpha==0 视为完全透明）。</param>
    public static AlphaMask BuildFromPng(string path, byte threshold = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

        var decoder = new PngBitmapDecoder(
            new Uri(path, UriKind.Absolute),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
            throw new InvalidOperationException($"PNG 无图像帧: {path}");

        // 统一转换为 BGRA32，确保 alpha 位于每像素第 4 字节。
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

        int w = converted.PixelWidth;
        int h = converted.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[h * stride];
        converted.CopyPixels(pixels, stride, 0);

        var alpha = new byte[w * h];
        for (int i = 0; i < w * h; i++)
        {
            alpha[i] = pixels[i * 4 + 3]; // BGRA -> alpha at index 3
        }

        return new AlphaMask(w, h, alpha, threshold);
    }
}
