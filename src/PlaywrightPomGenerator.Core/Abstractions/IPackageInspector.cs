using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Reads workspace package facts (package.json, angular.json) that inform
/// analysis and generation. Pure file/JSON reading — works identically whether
/// the AST or regex analysis engine runs.
/// </summary>
public interface IPackageInspector
{
    /// <summary>
    /// Inspects the workspace rooted at <paramref name="rootPath"/>.
    /// Missing or malformed files yield an empty report, never an exception.
    /// </summary>
    /// <param name="rootPath">The workspace root (directory containing package.json).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The package report.</returns>
    Task<PackageReport> InspectAsync(string rootPath, CancellationToken cancellationToken = default);
}
