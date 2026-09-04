using System;
using System.Collections;
using System.Collections.Generic;

namespace Prismica.Core.Parameters;

/// <summary>
/// 全局变量存储（跨组件共享的字符串字典）。
/// <para>
/// 早期架构 F9：组件 A 写入 <c>gv:Name</c>，组件 B 经 <c>#gv:Name#</c> 实时读取。
/// 典型用法：组件 .pri 的 <c>[GlobalVariables]</c> 段声明初值（加载时 seed 进共享存储）；
/// 运行期可调用 <see cref="Set"/> 写入，下一次渲染中 <c>#gv:Name#</c> 即反映最新值（实时联动）。
/// </para>
/// <para>
/// 实现 <see cref="IReadOnlyDictionary{TKey,TValue}"/>，以便直接作为 <c>MeterContext.Globals</c> 注入；
/// 度量/文本替换只读取，不修改其中的值。
/// </para>
/// </summary>
public sealed class GlobalVariableStore : IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>读取变量值；不存在返回 null。</summary>
    public string? Get(string name) => _store.TryGetValue(name, out var v) ? v : null;

    /// <summary>写入（覆盖）变量值。null 值按空字符串处理。</summary>
    public void Set(string name, string value) => _store[name] = value ?? "";

    /// <summary>仅当变量尚不存在时写入；用于 .pri 加载时的初值 seed，避免覆盖运行期已变更的值。返回是否真正写入。</summary>
    public bool TryAdd(string name, string value)
    {
        if (_store.ContainsKey(name)) return false;
        _store[name] = value ?? "";
        return true;
    }

    /// <summary>移除变量；不存在返回 false。</summary>
    public bool Remove(string name) => _store.Remove(name);

    /// <summary>清空所有变量。</summary>
    public void Clear() => _store.Clear();

    /// <summary>返回当前所有变量的快照副本（大小写不敏感键）。</summary>
    public IReadOnlyDictionary<string, string> Snapshot()
        => new Dictionary<string, string>(_store, StringComparer.OrdinalIgnoreCase);

    // ===== IReadOnlyDictionary<string,string> 实现 =====
    public string this[string key] => _store[key];
    public IEnumerable<string> Keys => _store.Keys;
    public IEnumerable<string> Values => _store.Values;
    public int Count => _store.Count;
    public bool ContainsKey(string key) => _store.ContainsKey(key);
    public bool TryGetValue(string key, out string value) => _store.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _store.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _store.GetEnumerator();
}
