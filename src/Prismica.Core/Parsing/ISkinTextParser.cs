using System;
using System.Collections.Generic;
using Prismica.Core.Components;
using Prismica.Core.Primitives;

namespace Prismica.Core.Parsing;

public interface ISkinTextParser
{
    ParseResult Parse(string text, string filePath = "<memory>");
    ParseResult ParseIncremental(string text, ParseResult previous);
}

public sealed record ParseResult(
    ComponentDefinition? Definition,
    IReadOnlyList<Diagnostic> Diagnostics
)
{
    public bool Success => Definition != null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}

public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string Message,
    string FilePath,
    int Line,
    int Column,
    int Length,
    string? Code = null
);

public enum DiagnosticSeverity { Info, Warning, Error }