using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Prismica.Core.Formula;

/// <summary>公式诊断（位置 + 信息），供编辑器高亮错误。</summary>
public sealed record FormulaDiagnostic(int Start, int Length, string Message);

/// <summary>
/// 公式语法校验（纯逻辑，可单测）。用 FormulaParser 试解析，捕获 FormatException 并提取错误位置。
/// </summary>
public static class FormulaValidator
{
    private static readonly Regex PositionRegex = new(@"position\s+(\d+)", RegexOptions.Compiled);

    public static IReadOnlyList<FormulaDiagnostic> Validate(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return new FormulaDiagnostic[0];

        try
        {
            _ = new FormulaParser(formula).Parse();
            return new FormulaDiagnostic[0];
        }
        catch (System.FormatException ex)
        {
            var m = PositionRegex.Match(ex.Message);
            int pos = m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            int len = pos < formula.Length ? 1 : 0;
            return new[] { new FormulaDiagnostic(pos, len, ex.Message) };
        }
    }
}
