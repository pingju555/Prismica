namespace Prismica.Core.Update;

/// <summary>
/// 语义化版本（简化实现）：<c>major.minor.patch[-prerelease]</c>。
/// 支持 <c>v1.2.3</c> / <c>1.2.3</c> / <c>1.2.3-beta.1</c> 等写法。
/// 比较规则：先比 major.minor.patch，再比 prerelease——同号下正式版高于预发布版，
/// 两个预发布版按 <c>.</c> 分段比较（数字段按数值、非数字段按字典序，近似 semver）。
/// </summary>
public sealed record SemVersion(int Major, int Minor, int Patch, string? Prerelease = null)
    : IComparable<SemVersion>
{
    /// <summary>解析版本字符串；非法输入返回 false 且 <paramref name="version"/> 为 null。</summary>
    public static bool TryParse(string? text, out SemVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text!.Trim();
        if (s.Length > 0 && (s[0] is 'v' or 'V')) s = s[1..];

        string core = s;
        string? pre = null;
        int dash = core.IndexOf('-');
        if (dash >= 0)
        {
            pre = core[(dash + 1)..];
            core = core[..dash];
        }

        var parts = core.Split('.');
        if (parts.Length is < 1 or > 3) return false;
        if (!int.TryParse(parts[0], out int major) || major < 0) return false;

        int minor = 0, patch = 0;
        if (parts.Length > 1 && (!int.TryParse(parts[1], out minor) || minor < 0)) return false;
        if (parts.Length > 2 && (!int.TryParse(parts[2], out patch) || patch < 0)) return false;

        version = new SemVersion(major, minor, patch, string.IsNullOrWhiteSpace(pre) ? null : pre.Trim());
        return true;
    }

    /// <summary>是否为预发布版本（带 prerelease 标识）。</summary>
    public bool IsPrerelease => Prerelease is not null;

    /// <inheritdoc />
    public int CompareTo(SemVersion? other)
    {
        if (other is null) return 1;
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;

        bool thisPre = IsPrerelease;
        bool otherPre = other.IsPrerelease;
        if (!thisPre && !otherPre) return 0;
        if (!thisPre) return 1;   // 正式版 > 预发布
        if (!otherPre) return -1;
        return ComparePrerelease(Prerelease!, other.Prerelease!);
    }

    private static int ComparePrerelease(string a, string b)
    {
        var aa = a.Split('.');
        var bb = b.Split('.');
        int n = Math.Min(aa.Length, bb.Length);
        for (int i = 0; i < n; i++)
        {
            bool aNum = int.TryParse(aa[i], out int an);
            bool bNum = int.TryParse(bb[i], out int bn);
            int c;
            if (aNum && bNum) c = an.CompareTo(bn);
            else if (aNum) c = -1;  // 数字段 < 标识符段
            else if (bNum) c = 1;
            else c = string.CompareOrdinal(aa[i], bb[i]);
            if (c != 0) return c;
        }
        return aa.Length.CompareTo(bb.Length);
    }

    /// <inheritdoc />
    public override string ToString() =>
        Prerelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Prerelease}";
}
