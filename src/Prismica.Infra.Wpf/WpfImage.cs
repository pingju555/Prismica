using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Prismica.Core.Native;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;

namespace Prismica.Infra.Wpf;

/// <summary>
/// IImage 的 WPF 实现：包装一个 BitmapSource 供 IRenderContext.DrawImage 使用。
/// 不继承 BitmapSource（避开大量抽象成员），由 WpfRenderContext 解包其 Source 绘制。
/// </summary>
public sealed class WpfImage : IImage
{
    public BitmapSource Source { get; }
    public WpfImage(BitmapSource source) => Source = source;
    public Prismica.Core.Primitives.Size Size => new(Source.PixelWidth, Source.PixelHeight);
    public double DpiX => Source.DpiX;
    public double DpiY => Source.DpiY;
    public void Dispose() { }
}

/// <summary>把 IconData（ArgbColor[] 像素，A 在高位）转换为可绘制的 IImage（BGRA32）。</summary>
public static class WpfImageFactory
{
    private static readonly WpfImage Empty = new(new BitmapImage());

    public static IImage FromIconData(IconData data)
    {
        if (data.Width <= 0 || data.Height <= 0 || data.Pixels.Length == 0)
            return Empty;

        var wb = new WriteableBitmap(data.Width, data.Height, 96, 96, PixelFormats.Bgra32, null);
        int count = data.Width * data.Height;
        var bytes = new byte[count * 4];
        for (int i = 0; i < count; i++)
        {
            var c = data.Pixels[i];
            bytes[i * 4 + 0] = c.B;
            bytes[i * 4 + 1] = c.G;
            bytes[i * 4 + 2] = c.R;
            bytes[i * 4 + 3] = c.A;
        }
        wb.WritePixels(new Int32Rect(0, 0, data.Width, data.Height), bytes, data.Width * 4, 0);
        wb.Freeze();
        return new WpfImage(wb);
    }
}
