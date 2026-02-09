using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Defines the contract for analyzing Angular applications and workspaces.
/// </summary>
public interface IAngularAnalyzer
{
    /// <summary>
    /// Analyzes an Angular workspace at the specified path.
    /// </summary>
    /// <param name="workspacePath">The path to the Angular workspace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the Angular workspace.</returns>
    Task<AngularWorkspaceInfo> AnalyzeWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a single Angular application at the specified path.
    /// </summary>
    /// <param name="applicationPath">The path to the Angular application.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the Angular project.</returns>
    Task<AngularProjectInfo> AnalyzeApplicationAsync(
        string applicationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the specified path is an Angular workspace.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is an Angular workspace, false otherwise.</returns>
    bool IsWorkspace(string path);

    /// <summary>
    /// Determines if the specified path is an Angular application.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is an Angular application, false otherwise.</returns>
    bool IsApplication(string path);

    /// <summary>
    /// Determines if the specified path is an Angular library.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is an Angular library (has ng-package.json), false otherwise.</returns>
    bool IsLibrary(string path);

    /// <summary>
    /// Analyzes a single Angular library at the specified path.
    /// </summary>
    /// <param name="libraryPath">The path to the Angular library.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the Angular library project.</returns>
    Task<AngularProjectInfo> AnalyzeLibraryAsync(
        string libraryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes Angular components at an arbitrary path (file or directory) without
    /// requiring angular.json or package.json. Useful for analyzing remote repositories
    /// where the URL points to a specific component file or folder.
    /// </summary>
    /// <param name="targetPath">The path to the file or directory to analyze.</param>
    /// <param name="projectName">The name to use for the generated project.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the discovered components.</returns>
    Task<AngularProjectInfo> AnalyzeComponentsAtPathAsync(
        string targetPath,
        string projectName,
        CancellationToken cancellationToken = default);
}
