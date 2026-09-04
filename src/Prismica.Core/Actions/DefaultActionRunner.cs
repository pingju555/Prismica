
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Prismica.Core.Primitives;
using Prismica.Core.Formula;
using Prismica.Core.Measures;
using Prismica.Core.Meters;
using Prismica.Core.Components;
using Prismica.Core.Native;

namespace Prismica.Core.Actions;

public sealed class DefaultActionRunner : IActionRunner
{
    public async ValueTask<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext ctx)
    {
        try
        {
            if (!string.IsNullOrEmpty(action.Condition))
            {
                var condAst = ctx.FormulaEngine.Parse(action.Condition);
                var condResult = ctx.FormulaEngine.Evaluate(condAst, CreateEvalContext(ctx));
                if (!condResult.AsBool()) return new ActionResult(true, null, "skipped");
            }

            return action.Kind switch
            {
                ActionKind.SetVariable => ExecuteSetVariable(action, ctx),
                ActionKind.SetParameter => ExecuteSetParameter(action, ctx),
                ActionKind.ExecuteCommand => ExecuteCommand(action, ctx),
                ActionKind.OpenUrl => ExecuteOpenUrl(action, ctx),
                ActionKind.OpenFile => ExecuteOpenFile(action, ctx),
                ActionKind.Delay => await ExecuteDelay(action, ctx),
                ActionKind.RefreshMeasure => await ExecuteRefreshMeasure(action, ctx),
                ActionKind.RefreshEmbed => ExecuteRefreshEmbed(action, ctx),
                _ => new ActionResult(false, $"Unsupported action: {action.Kind}", null)
            };
        }
        catch (Exception ex)
        {
            return new ActionResult(false, ex.Message, null);
        }
    }

    private Formula.EvalContext CreateEvalContext(ActionContext ctx)
    {
        return new Formula.EvalContext(
            ConvertVariables(ctx.Variables),
            ctx.Measures,
            new Dictionary<string, object>(),
            ctx.CancellationToken
        );
    }

    private static IReadOnlyDictionary<string, FormulaValue> ConvertVariables(IReadOnlyDictionary<string, ArgbColor> vars)
    {
        var dict = new Dictionary<string, FormulaValue>();
        foreach (var kvp in vars)
            dict[kvp.Key] = FormulaValue.FromString(kvp.Value.ToHex());
        return dict;
    }

    public async ValueTask<FlowResult> RunFlowAsync(FlowDefinition flow, ActionContext ctx)
    {
        int executed = 0;
        for (int i = 0; i < flow.Steps.Count; i++)
        {
            var step = flow.Steps[i];
            if (!string.IsNullOrEmpty(step.Condition))
            {
                var condAst = ctx.FormulaEngine.Parse(step.Condition);
                var condResult = ctx.FormulaEngine.Evaluate(condAst, CreateEvalContext(ctx));
                if (!condResult.AsBool()) continue;
            }

            var result = await ExecuteAsync(step.Action, ctx);
            executed++;
            if (!result.Success) return new FlowResult(false, executed, result.Error);

            if (step.LoopToStepIndex.HasValue && step.LoopToStepIndex.Value >= 0 && step.LoopToStepIndex.Value < i)
                i = step.LoopToStepIndex.Value - 1;
        }
        return new FlowResult(true, executed, null);
    }

    private ActionResult ExecuteSetVariable(ActionDefinition action, ActionContext ctx)
    {
        if (action.Parameters.TryGetValue("Name", out var nameObj) && action.Parameters.TryGetValue("Value", out var valObj))
        {
            string name = nameObj.ToString()!;
            var val = valObj switch
            {
                double d => Formula.FormulaValue.FromNumber(d),
                string s => Formula.FormulaValue.FromString(s),
                bool b => Formula.FormulaValue.FromBool(b),
                _ => Formula.FormulaValue.FromString(valObj.ToString()!)
            };
            return new ActionResult(true, null, $"Set {name}={val}");
        }
        return new ActionResult(false, "Missing Name/Value", null);
    }

    private ActionResult ExecuteSetParameter(ActionDefinition action, ActionContext ctx)
    {
        return new ActionResult(true, null, "SetParameter (stub)");
    }

    private ActionResult ExecuteCommand(ActionDefinition action, ActionContext ctx)
    {
        if (action.Parameters.TryGetValue("Command", out var cmdObj))
        {
            string cmd = cmdObj.ToString()!;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {cmd}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
                return new ActionResult(true, null, $"Executed: {cmd}");
            }
            catch (Exception ex) { return new ActionResult(false, ex.Message, null); }
        }
        return new ActionResult(false, "Missing Command", null);
    }

    private ActionResult ExecuteOpenUrl(ActionDefinition action, ActionContext ctx)
    {
        if (action.Parameters.TryGetValue("Url", out var urlObj))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(urlObj.ToString()!) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
                return new ActionResult(true, null, "Opened URL");
            }
            catch (Exception ex) { return new ActionResult(false, ex.Message, null); }
        }
        return new ActionResult(false, "Missing Url", null);
    }

    private ActionResult ExecuteOpenFile(ActionDefinition action, ActionContext ctx)
    {
        if (action.Parameters.TryGetValue("Path", out var pathObj))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(pathObj.ToString()!) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
                return new ActionResult(true, null, "Opened file");
            }
            catch (Exception ex) { return new ActionResult(false, ex.Message, null); }
        }
        return new ActionResult(false, "Missing Path", null);
    }

    private async ValueTask<ActionResult> ExecuteDelay(ActionDefinition action, ActionContext ctx)
    {
        if (action.Parameters.TryGetValue("Ms", out var msObj) && int.TryParse(msObj.ToString(), out var ms))
        {
            await Task.Delay(ms, ctx.CancellationToken);
            return new ActionResult(true, null, $"Delayed {ms}ms");
        }
        return new ActionResult(false, "Invalid Ms", null);
    }

    private async ValueTask<ActionResult> ExecuteRefreshMeasure(ActionDefinition action, ActionContext ctx)
    {
        if (action.Parameters.TryGetValue("Name", out var nameObj))
        {
            string name = nameObj.ToString()!;
            if (ctx.Measures.TryGetValue(name, out var measure))
            {
                var evalCtx = new Formula.EvalContext(
                    new Dictionary<string, Formula.FormulaValue>(),
                    ctx.Measures,
                    new Dictionary<string, object>(),
                    ctx.CancellationToken
                );
                await measure.UpdateAsync(new MeasureContext(
                    ctx.Measures,
                    new Dictionary<string, ArgbColor>(),
                    ctx.FormulaEngine,
                    TimeSpan.Zero,
                    ctx.CancellationToken
                ));
                return new ActionResult(true, null, $"Refreshed {name}");
            }
        }
        return new ActionResult(false, "Measure not found", null);
    }

    private ActionResult ExecuteRefreshEmbed(ActionDefinition action, ActionContext ctx)
    {
        return new ActionResult(true, null, "RefreshEmbed (stub)");
    }
}
