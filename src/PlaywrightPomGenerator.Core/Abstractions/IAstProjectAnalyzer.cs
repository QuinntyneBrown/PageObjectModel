using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Whole-project AST analysis via the Node sidecar's batched analyzeProject
/// method: component metadata, template element facts, route tree, and engine
/// versions. Distinct from <see cref="ITypeScriptAnalyzer"/>, which is the
/// bridge command's InjectionToken seam.
/// </summary>
public interface IAstProjectAnalyzer
{
    /// <summary>
    /// Analyzes the given projects in one sidecar invocation.
    /// </summary>
    /// <param name="rootPath">The workspace root (used for NODE_PATH and tsconfig resolution).</param>
    /// <param name="projects">The projects (name + source root + prefix) to analyze.</param>
    /// <param name="includeTemplateContent">Whether raw template text is echoed back (debug only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mapped analysis result.</returns>
    /// <exception cref="Services.SidecarUnavailableException">When Node/sidecar/typescript cannot run.</exception>
    /// <exception cref="InvalidOperationException">On RPC-level or schema errors.</exception>
    Task<AstProjectAnalysis> AnalyzeProjectAsync(
        string rootPath,
        IReadOnlyList<AstProjectTarget> projects,
        bool includeTemplateContent = false,
        CancellationToken cancellationToken = default);
}
