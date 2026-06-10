namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Facts read from an Angular build output (dist) directory: deployment truth
/// that complements source analysis.
/// </summary>
public sealed record DistAnalysis
{
    /// <summary>
    /// Gets the dist directory that was analyzed.
    /// </summary>
    public required string DistPath { get; init; }

    /// <summary>
    /// Gets the &lt;base href&gt; from index.html, when present.
    /// </summary>
    public string? BaseHref { get; init; }

    /// <summary>
    /// Gets route paths confirmed by prerendered index.html directories.
    /// Absence of a route here means nothing (prerendering is selective).
    /// </summary>
    public IReadOnlyList<string> PrerenderedRoutes { get; init; } = [];
}
