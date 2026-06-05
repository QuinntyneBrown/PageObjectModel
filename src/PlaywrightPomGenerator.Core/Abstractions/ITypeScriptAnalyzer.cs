using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Analyzes TypeScript source using the TypeScript compiler (via the Node AST sidecar) to discover
/// service interfaces registered through <c>InjectionToken</c> and resolve their members
/// (including members inherited via <c>extends</c>).
/// </summary>
public interface ITypeScriptAnalyzer
{
    /// <summary>
    /// Discovers the injection-token-backed service interfaces under a path.
    /// </summary>
    /// <param name="path">The directory to scan (workspace, application, library, or folder).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovered interfaces with resolved members.</returns>
    Task<IReadOnlyList<InjectionTokenInterface>> DiscoverInjectionTokenInterfacesAsync(
        string path,
        CancellationToken cancellationToken = default);
}
