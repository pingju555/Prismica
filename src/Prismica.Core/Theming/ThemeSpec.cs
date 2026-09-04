namespace Prismica.Core.Theming;

/// <summary>
/// 组件级主题规格：一个命名的令牌集合。
/// 令牌值统一以字符串保存（颜色 <c>#AARRGGBB</c>、字体名、数字、文本皆可），
/// 解析期做纯文本替换，故无需区分类型。
/// 对应 <c>.pri</c> 的 <c>[Theme.&lt;Name&gt;]</c> 段。
/// </summary>
public sealed record ThemeSpec(string Name, IReadOnlyDictionary<string, string> Tokens);
