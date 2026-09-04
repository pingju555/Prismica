using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using CorePoint = Prismica.Core.Primitives.Point;
using CoreRect = Prismica.Core.Primitives.Rect;
using CoreResult = Prismica.Core.Components.HitTestResult;
using WpfSize = System.Windows.Size;
using WpfRect = System.Windows.Rect;

namespace Prismica.Infra.Wpf;

/// <summary>IRenderHost 的 WPF 实现（WP3 原型切片）。提供视觉根创建、更新、布局、命中测试与离屏截图。</summary>
public sealed class WpfRenderHost : IRenderHost
{
    public IVisualRoot CreateVisualRoot(ComponentDefinition def, RenderContext ctx)
        => new WpfVisualRoot(def, ctx);

    public void UpdateVisual(IVisualRoot root, ParameterOverride overrides)
    {
        if (root is not WpfVisualRoot wpf) return;
        if (overrides.Values != null)
        {
            foreach (var kv in overrides.Values)
            {
                switch (kv.Key.ToLowerInvariant())
                {
                    case "opacity" when kv.Value is double d: ((IVisual)wpf).Opacity = d; break;
                    case "visible" when kv.Value is bool b: ((IVisual)wpf).IsVisible = b; break;
                }
            }
        }
        ((IVisualRoot)wpf).InvalidateVisual();
    }

    public void ArrangeLayout(IVisualRoot root, CoreRect finalRect)
    {
        if (root is not FrameworkElement fe) return;
        fe.Measure(new WpfSize(finalRect.Width, finalRect.Height));
        fe.Arrange(new WpfRect(0, 0, finalRect.Width, finalRect.Height));
    }

    public CoreResult HitTest(IVisualRoot root, CorePoint point)
        => root.HitTest(point);

    public Task<byte[]> CaptureAsync(IVisualRoot root, ImageFormat format = ImageFormat.Png)
    {
        if (root is not FrameworkElement fe) return Task.FromResult(Array.Empty<byte>());
        double w = Math.Max(1, fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width);
        double h = Math.Max(1, fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height);

        var target = new RenderTargetBitmap((int)Math.Ceiling(w), (int)Math.Ceiling(h), 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var visualBrush = new VisualBrush(fe) { Stretch = Stretch.Fill };
            dc.DrawRectangle(visualBrush, null, new WpfRect(0, 0, w, h));
        }
        target.Render(dv);

        var encoder = format switch
        {
            ImageFormat.Jpeg => (BitmapEncoder)new JpegBitmapEncoder(),
            ImageFormat.Bmp => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
        encoder.Frames.Add(BitmapFrame.Create(target));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return Task.FromResult(ms.ToArray());
    }

    public void Dispose() { }
}