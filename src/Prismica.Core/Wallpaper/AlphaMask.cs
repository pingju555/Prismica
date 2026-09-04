namespace Prismica.Core.Wallpaper;

/// <summary>
/// 壁纸图片的预计算 alpha 遮罩（per-pixel）。
/// 在加载/换图时一次性扫描 PNG 的 alpha 通道得到，点击命中时 O(1) 查表，
/// 无需逐帧回读渲染缓冲——满足"预识别缓存做形状遮罩"的需求。
/// alpha &lt;= <see cref="Threshold"/> 的像素视为完全透明（应点击穿透），其余视为不透明（应接收点击）。
/// </summary>
public sealed class AlphaMask
{
    private readonly byte[] _alpha; // 长度 = Width*Height，每元素 0-255

    /// <summary>遮罩宽度（像素）。</summary>
    public int Width { get; }

    /// <summary>遮罩高度（像素）。</summary>
    public int Height { get; }

    /// <summary>透明阈值：alpha &lt;= 该值视为完全透明（穿透）。默认 0 = 仅严格完全透明（alpha==0）才穿透。</summary>
    public byte Threshold { get; }

    public AlphaMask(int width, int height, byte[] alpha, byte threshold = 0)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (alpha is null) throw new ArgumentNullException(nameof(alpha));
        if (alpha.Length != width * height)
            throw new ArgumentException($"alpha 长度 {alpha.Length} 与 {width}x{height} 不匹配", nameof(alpha));

        Width = width;
        Height = height;
        _alpha = alpha;
        Threshold = threshold;
    }

    /// <summary>读取某像素的 alpha 值（0-255）；越界返回 0（视为透明）。</summary>
    public byte AlphaAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        return _alpha[y * Width + x];
    }

    /// <summary>
    /// 判断像素 (x, y) 是否透明、应点击穿透到下层桌面。
    /// 越界或 alpha &lt;= <see cref="Threshold"/> 返回 true；否则返回 false（不透明、应接收点击）。
    /// </summary>
    public bool IsTransparent(int x, int y) => AlphaAt(x, y) <= Threshold;
}
