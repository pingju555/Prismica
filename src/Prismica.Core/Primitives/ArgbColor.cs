namespace Prismica.Core.Primitives;

/// <summary>
/// ARGB 颜色（Premultiplied Alpha，32 位无符号整数存储）。
/// 无 WPF 依赖，Core 纯值类型。
/// </summary>
public readonly record struct ArgbColor(uint Value)
{
    public byte A => (byte)(Value >> 24);
    public byte R => (byte)(Value >> 16);
    public byte G => (byte)(Value >> 8);
    public byte B => (byte)Value;

    public bool IsTransparent => A == 0;
    public bool IsOpaque => A == 255;

    public static ArgbColor FromRgba(byte r, byte g, byte b, byte a = 255) =>
        new((uint)(a << 24 | r << 16 | g << 8 | b));

    public static ArgbColor FromRgb(byte r, byte g, byte b) =>
        new((uint)(0xFF << 24 | r << 16 | g << 8 | b));

    public static ArgbColor FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8) throw new FormatException($"Invalid ARGB hex: {hex}");
        return new(uint.Parse(hex, System.Globalization.NumberStyles.HexNumber));
    }

    public string ToHex(bool includeAlpha = true) =>
        includeAlpha ? $"#{Value:X8}" : $"#{Value & 0xFFFFFF:X6}";

    public static implicit operator uint(ArgbColor c) => c.Value;
    public static implicit operator ArgbColor(uint v) => new(v);

    public override string ToString() => ToHex();

    // 常用颜色
    public static readonly ArgbColor Transparent = new(0x00000000);
    public static readonly ArgbColor Black = new(0xFF000000);
    public static readonly ArgbColor White = new(0xFFFFFFFF);
    public static readonly ArgbColor Red = new(0xFFFF0000);
    public static readonly ArgbColor Green = new(0xFF00FF00);
    public static readonly ArgbColor Blue = new(0xFF0000FF);
}