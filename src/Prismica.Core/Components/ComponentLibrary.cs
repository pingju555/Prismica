using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Prismica.Core.Parsing;

namespace Prismica.Core.Components;

/// <summary>
/// 扫描 Components 目录，列出可用的 .pri 组件。
/// </summary>
public sealed class ComponentLibrary
{
    private readonly string _componentsDir;
    private readonly IniSkinTextParser _parser = new();

    public ComponentLibrary(string componentsDir)
    {
        _componentsDir = componentsDir;
        Directory.CreateDirectory(_componentsDir);
    }

    /// <summary>
    /// 获取所有可用组件的元数据。
    /// </summary>
    public IReadOnlyList<ComponentInfo> GetAvailableComponents()
    {
        var result = new List<ComponentInfo>();
        foreach (var file in Directory.GetFiles(_componentsDir, "*.pri", SearchOption.AllDirectories))
        {
            try
            {
                var text = File.ReadAllText(file);
                var parseResult = _parser.Parse(text, file);
                if (parseResult.Definition is null) continue;

                var def = parseResult.Definition;
                result.Add(new ComponentInfo
                {
                    Name = def.Name,
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Author = def.Prismica.Author,
                    Description = def.Prismica.Description,
                    Version = def.Prismica.Version,
                    DefaultWidth = (int)def.Prismica.Width,
                    DefaultHeight = (int)def.Prismica.Height
                });
            }
            catch
            {
                // 跳过解析失败的文件
            }
        }
        return result.OrderBy(c => c.Name).ToList();
    }

    /// <summary>
    /// 根据名称查找组件。
    /// </summary>
    public ComponentInfo? FindComponent(string name)
    {
        return GetAvailableComponents()
            .FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取组件文件的完整路径。
    /// </summary>
    public string GetFilePath(string componentName)
    {
        return Path.Combine(_componentsDir, $"{componentName}.pri");
    }
}

/// <summary>
/// 组件元数据。
/// </summary>
public sealed class ComponentInfo
{
    public required string Name { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public string Author { get; init; } = "";
    public string Description { get; init; } = "";
    public string Version { get; init; } = "0.1";
    public int DefaultWidth { get; init; } = 400;
    public int DefaultHeight { get; init; } = 300;
}
