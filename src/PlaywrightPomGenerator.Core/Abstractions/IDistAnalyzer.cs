using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Reads facts from an Angular build output (dist) directory: the &lt;base href&gt;
/// from index.html and prerendered route directories. Deployment truth that
/// complements source analysis — absence of dist output is never an error.
/// </summary>
public interface IDistAnalyzer
{
    /// <summary>
    /// Analyzes the dist output for a project. With no explicit path, probes
    /// dist/{project}/browser, dist/{project}, then dist under the project root.
    /// </summary>
    /// <param name="projectRoot">The workspace/project root.</param>
    /// <param name="projectName">The project name (for dist/{project} probing).</param>
    /// <param name="explicitDistPath">An explicit dist directory (--dist), or null to auto-detect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dist facts, or null when no dist output was found.</returns>
    Task<DistAnalysis?> AnalyzeAsync(
        string projectRoot,
        string projectName,
        string? explicitDistPath = null,
        CancellationToken cancellationToken = default);
}
