namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Describes which analysis engine actually ran and why, for user-facing
/// reporting by the command handlers.
/// </summary>
public sealed record AnalysisReport
{
    /// <summary>
    /// Gets the engine that was requested (options/CLI).
    /// </summary>
    public required AnalysisEngine EngineRequested { get; init; }

    /// <summary>
    /// Gets the engine that actually produced the analysis.
    /// </summary>
    public required AnalysisEngineUsed EngineUsed { get; init; }

    /// <summary>
    /// Gets the TypeScript version the sidecar resolved, when AST analysis ran.
    /// </summary>
    public string? TypeScriptVersion { get; init; }

    /// <summary>
    /// Gets the @angular/compiler version the sidecar resolved, when template
    /// AST analysis ran.
    /// </summary>
    public string? AngularCompilerVersion { get; init; }

    /// <summary>
    /// Gets the human-readable reason for a fallback to regex analysis, if any.
    /// </summary>
    public string? FallbackReason { get; init; }

    /// <summary>
    /// Gets analysis warnings to surface to the user.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// The analysis path that actually ran.
/// </summary>
public enum AnalysisEngineUsed
{
    /// <summary>
    /// Regex/string analysis.
    /// </summary>
    Regex,

    /// <summary>
    /// Full AST analysis (TypeScript facts and template AST).
    /// </summary>
    Ast,

    /// <summary>
    /// AST analysis for TypeScript facts with regex template parsing
    /// (@angular/compiler unavailable).
    /// </summary>
    AstWithRegexTemplates
}
