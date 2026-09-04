using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Prismica.Core.Primitives;
using Prismica.Core.Components;

namespace Prismica.Core.Persistence;

public sealed class IniLayoutSerializer : ILayoutSerializer
{
    public LayoutDocument Deserialize(Stream stream, LayoutFormat format)
    {
        using var reader = new StreamReader(stream);
        string text = reader.ReadToEnd();
        return format switch
        {
            LayoutFormat.Ini => ParseIni(text),
            LayoutFormat.Json => System.Text.Json.JsonSerializer.Deserialize<LayoutDocument>(text) ?? EmptyDoc(),
            _ => EmptyDoc()
        };
    }

    public void Serialize(LayoutDocument doc, Stream stream, LayoutFormat format)
    {
        using var writer = new StreamWriter(stream);
        switch (format)
        {
            case LayoutFormat.Ini:
                writer.Write(SerializeIni(doc));
                break;
            case LayoutFormat.Json:
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                writer.Write(System.Text.Json.JsonSerializer.Serialize(doc, options));
                break;
        }
    }

    private LayoutDocument ParseIni(string text)
    {
        var lines = text.Split('\n');
        string? currentSection = null;
        var sectionData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#')) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!sectionData.ContainsKey(currentSection))
                    sectionData[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (currentSection == null) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            sectionData[currentSection].TryAdd(key, value);
        }

        var metadata = new LayoutMetadata("", "", "", DateTime.UtcNow, DateTime.UtcNow, null);
        if (sectionData.TryGetValue("Layout", out var layoutDict))
        {
            metadata = new LayoutMetadata(
                layoutDict.GetValueOrDefault("Name", ""),
                layoutDict.GetValueOrDefault("Author", ""),
                layoutDict.GetValueOrDefault("Description", ""),
                DateTime.TryParse(layoutDict.GetValueOrDefault("Created", ""), out var c) ? c : DateTime.UtcNow,
                DateTime.TryParse(layoutDict.GetValueOrDefault("Modified", ""), out var m) ? m : DateTime.UtcNow,
                layoutDict.TryGetValue("WallpaperPath", out var wp) ? wp : null
            );
        }

        var instances = new List<ComponentInstance>();
        foreach (var (section, dict) in sectionData)
        {
            if (!section.StartsWith("Instance_", StringComparison.OrdinalIgnoreCase)) continue;
            string id = section["Instance_".Length..];
            bool enabled = true;
            if (dict.TryGetValue("Enabled", out var enStr) && bool.TryParse(enStr, out var en))
                enabled = en;
            var inst = new ComponentInstance(
                id,
                dict.GetValueOrDefault("Component", ""),
                ParseRect(dict.GetValueOrDefault("Bounds", "0,0,200,100")),
                int.TryParse(dict.GetValueOrDefault("ZIndex", "0"), out var z) ? z : 0,
                dict.Where(kvp => kvp.Key.StartsWith("Interface.", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(k => k.Key["Interface.".Length..], v => (object)v.Value, StringComparer.OrdinalIgnoreCase),
                enabled
            );
            instances.Add(inst);
        }

        return new LayoutDocument("0.1", metadata, instances);
    }

    private string SerializeIni(LayoutDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Layout]");
        sb.AppendLine($"Version={doc.Version}");
        sb.AppendLine($"Name={doc.Metadata.Name}");
        sb.AppendLine($"Author={doc.Metadata.Author}");
        sb.AppendLine($"Description={doc.Metadata.Description}");
        sb.AppendLine($"Created={doc.Metadata.Created:O}");
        sb.AppendLine($"Modified={doc.Metadata.Modified:O}");
        if (!string.IsNullOrEmpty(doc.Metadata.WallpaperPath))
            sb.AppendLine($"WallpaperPath={doc.Metadata.WallpaperPath}");
        sb.AppendLine();

        foreach (var inst in doc.Instances)
        {
            sb.AppendLine($"[Instance_{inst.Id}]");
            sb.AppendLine($"Component={inst.ComponentName}");
            sb.AppendLine($"Bounds={inst.Bounds.X},{inst.Bounds.Y},{inst.Bounds.Width},{inst.Bounds.Height}");
            sb.AppendLine($"ZIndex={inst.ZIndex}");
            foreach (var (k, v) in inst.ParameterOverrides)
                sb.AppendLine($"Interface.{k}={v}");
            sb.AppendLine($"Enabled={inst.Enabled}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static Rect ParseRect(string s)
    {
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 && parts.All(p => double.TryParse(p, out _)))
            return new Rect(double.Parse(parts[0]), double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3]));
        return new Rect(0, 0, 200, 100);
    }

    private static LayoutDocument EmptyDoc() => new("0.1", new LayoutMetadata("", "", "", DateTime.UtcNow, DateTime.UtcNow, null), Array.Empty<ComponentInstance>());
}