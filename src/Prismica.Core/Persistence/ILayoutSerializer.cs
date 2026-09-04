using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;
using Prismica.Core.Components;

namespace Prismica.Core.Persistence;

public interface ILayoutSerializer
{
    LayoutDocument Deserialize(Stream stream, LayoutFormat format);
    void Serialize(LayoutDocument doc, Stream stream, LayoutFormat format);
}

public enum LayoutFormat { Ini, Json, Binary }

public sealed record LayoutDocument(
    string Version,
    LayoutMetadata Metadata,
    IReadOnlyList<ComponentInstance> Instances
);

public sealed record LayoutMetadata(
    string Name,
    string Author,
    string Description,
    DateTime Created,
    DateTime Modified,
    string? WallpaperPath
);

public sealed record ComponentInstance(
    string Id,
    string ComponentName,
    Rect Bounds,
    int ZIndex,
    IReadOnlyDictionary<string, object> ParameterOverrides,
    bool Enabled = true
);