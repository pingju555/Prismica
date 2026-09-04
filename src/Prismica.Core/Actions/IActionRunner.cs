using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Prismica.Core.Primitives;
using Prismica.Core.Formula;
using Prismica.Core.Measures;
using Prismica.Core.Meters;
using Prismica.Core.Components;
using Prismica.Core.Native;

namespace Prismica.Core.Actions;

public interface IActionRunner
{
    ValueTask<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext ctx);
    ValueTask<FlowResult> RunFlowAsync(FlowDefinition flow, ActionContext ctx);
}

public sealed record ActionDefinition(
    ActionKind Kind,
    IReadOnlyDictionary<string, object> Parameters,
    string? Condition
);

public enum ActionKind
{
    SetVariable, SetParameter, ExecuteCommand, OpenUrl, OpenFile,
    PlaySound, ShowNotification, RunScript, Delay,
    If, While, ForEach, Break, Continue, Return,
    SetAnchor, Move, Resize, Show, Hide, Toggle,
    RefreshMeasure, RefreshEmbed, CaptureScreenshot
}

public sealed record ActionResult(bool Success, string? Error, object? Output);

public sealed record FlowDefinition(
    string Name,
    IReadOnlyList<FlowStep> Steps
);

public sealed record FlowStep(
    ActionDefinition Action,
    string? Condition,
    int? LoopToStepIndex
);

public sealed record FlowResult(bool Completed, int StepsExecuted, string? Error);

public sealed record ActionContext(
    IReadOnlyDictionary<string, IMeasure> Measures,
    IReadOnlyDictionary<string, IMeter> Meters,
    IReadOnlyDictionary<string, IEmbedHost> Embeds,
    IReadOnlyDictionary<string, ArgbColor> Variables,
    IFormulaEngine FormulaEngine,
    INativeDesktop Native,
    CancellationToken CancellationToken
);