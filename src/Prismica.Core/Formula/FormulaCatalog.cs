using System.Collections.Generic;

namespace Prismica.Core.Formula;

/// <summary>
/// 公式内建函数目录（单一真相源）。供公式编辑器展示函数签名/说明，也供引擎 GetFunctions 返回元数据。
/// 与 DefaultFormulaEngine.RegisterBuiltins 保持同步。
/// </summary>
public static class FormulaCatalog
{
    private static readonly IReadOnlyList<FunctionInfo> _all = new FunctionInfo[]
    {
        // 数学
        Info("abs", 1, 1, "绝对值", "n"),
        Info("ceil", 1, 1, "向上取整", "n"),
        Info("floor", 1, 1, "向下取整", "n"),
        Info("round", 1, 1, "四舍五入", "n"),
        Info("sqrt", 1, 1, "平方根", "n"),
        Info("min", 1, int.MaxValue, "最小值（可变参数）", "a", "b", "..."),
        Info("max", 1, int.MaxValue, "最大值（可变参数）", "a", "b", "..."),
        Info("clamp", 3, 3, "钳制到 [min,max]", "value", "min", "max"),
        Info("lerp", 3, 3, "线性插值 a→b 比例 t", "a", "b", "t"),
        Info("sin", 1, 1, "正弦（弧度）", "x"),
        Info("cos", 1, 1, "余弦（弧度）", "x"),
        Info("tan", 1, 1, "正切（弧度）", "x"),
        Info("asin", 1, 1, "反正弦", "x"),
        Info("acos", 1, 1, "反余弦", "x"),
        Info("atan", 1, 1, "反正切", "x"),
        Info("atan2", 2, 2, "y/x 的反正切", "y", "x"),
        Info("rad", 1, 1, "角度→弧度", "deg"),
        Info("deg", 1, 1, "弧度→角度", "rad"),
        Info("log", 1, 1, "自然对数", "n"),
        Info("log10", 1, 1, "常用对数", "n"),
        Info("exp", 1, 1, "e 的指数", "n"),
        Info("pow", 2, 2, "b 的 e 次幂", "base", "exp"),
        // 字符串
        Info("substr", 2, 3, "子串 s[start, start+len]", "s", "start", "len?"),
        Info("strlen", 1, 1, "字符串长度", "s"),
        Info("upper", 1, 1, "转大写", "s"),
        Info("lower", 1, 1, "转小写", "s"),
        Info("trim", 1, 1, "去首尾空白", "s"),
        Info("replace", 3, 3, "替换子串", "s", "old", "new"),
        Info("contains", 2, 2, "是否包含子串", "s", "sub"),
        Info("startswith", 2, 2, "是否以子串开头", "s", "sub"),
        Info("endswith", 2, 2, "是否以子串结尾", "s", "sub"),
        // 条件
        Info("if", 3, 3, "条件 ? a : b", "cond", "a", "b"),
        Info("iif", 3, 3, "同 if", "cond", "a", "b"),
        // 时间
        Info("time", 0, 1, "当前时间（格式串可选）"),
        Info("timestamp", 0, 0, "Unix 秒时间戳"),
    };

    private static FunctionInfo Info(string name, int min, int max, string desc, params string[] p)
        => new(name, min, max, desc, p);

    public static IReadOnlyList<FunctionInfo> All => _all;

    private static readonly Dictionary<string, FunctionInfo> _byName =
        _all.ToDictionary(f => f.Name, f => f, System.StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string name, out FunctionInfo? info)
        => _byName.TryGetValue(name, out info);
}
