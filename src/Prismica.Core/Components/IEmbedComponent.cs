using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Prismica.Core.Primitives;
using Prismica.Core.Formula;
using Prismica.Core.Measures;
using Prismica.Core.Native;
using Prismica.Core.Scheduling;
using Prismica.Core.Rendering;

namespace Prismica.Core.Components;

[Flags]
public enum EmbedCapabilities
{
    None = 0,
    Interactive = 1,
    Animatable = 2,
    HeavyGpu = 4,
    RequiresAdmin = 8,
    NetworkAccess = 16,
    FileAccess = 32,
    ShellIntegration = 64,
    Programmable = 128,
}

public interface IEmbedComponent : IDisposable
{
    string Keyword { get; }
    EmbedCapabilities Capabilities { get; }
    Size DefaultSize { get; }
    IEmbedHost CreateHost(EmbedDefinition def, EmbedContext ctx);
    IReadOnlyDictionary<string, EmbedPropSchema> GetPropsSchema();
    string GetMetaSchema();
}

public interface IEmbedHost : IDisposable
{
    EmbedDefinition Definition { get; }
    EmbedContext Context { get; }
    void OnFrame(FrameContext frame);
    void SetProps(IReadOnlyDictionary<string, object> props);
    void SetMeta(JsonNode meta);
    void Render(IVisualRoot root, RenderContext ctx, IRenderContext rc);
    HitTestResult HitTest(Point point);
    bool OnInput(InputEvent evt);
    EmbedStateSnapshot GetSnapshot();
}

public sealed record EmbedContext(
    IReadOnlyDictionary<string, IMeasure> Measures,
    IReadOnlyDictionary<string, ArgbColor> Variables,
    IFormulaEngine FormulaEngine,
    INativeDesktop? Native,
    CancellationToken CancellationToken
);

public sealed record EmbedPropSchema(
    string Key,
    EmbedPropType Type,
    object DefaultValue,
    string Description,
    double? Min, double? Max, double? Step,
    IReadOnlyList<string>? Options
);

public enum EmbedPropType { String, Number, Color, Font, Bool, Select, Slider, Url, Text, Json }

public sealed record EmbedStateSnapshot(
    string EmbedName,
    IReadOnlyDictionary<string, object> Props,
    JsonNode Meta
);

public sealed record HitTestResult(
    bool Hit,
    string? ElementId,
    HitTestAction Action,
    Point LocalPoint
);

public enum HitTestAction { None, Click, DoubleClick, Drag, ContextMenu, Hover }

public sealed record InputEvent(
    InputType Type,
    Point Position,
    MouseButton? Button,
    Key? Key,
    ModifierKeys Modifiers,
    int ClickCount,
    TimeSpan Timestamp
);

public enum InputType { MouseMove, MouseDown, MouseUp, MouseWheel, KeyDown, KeyUp, Touch, Gesture }
public enum MouseButton { Left, Right, Middle, XButton1, XButton2 }
public enum Key { None, Enter, Escape, Space, Tab, ArrowUp, ArrowDown, ArrowLeft, ArrowRight, F2, Delete, C, V, X, A, Z, Y }
[Flags] public enum ModifierKeys { None = 0, Ctrl = 1, Shift = 2, Alt = 4, Win = 8 }