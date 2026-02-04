using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Defines the contract for generating Playwright test code.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates all Playwright POM files for a single Angular application.
    /// </summary>
    /// <param name="project">The Angular project information.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the generation operation.</returns>
    Task<GenerationResult> GenerateForApplicationAsync(
        AngularProjectInfo project,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates all Playwright POM files for an Angular workspace.
    /// </summary>
    /// <param name="workspace">The Angular workspace information.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="projectName">Optional specific project name to generate for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the generation operation.</returns>
    Task<GenerationResult> GenerateForWorkspaceAsync(
        AngularWorkspaceInfo workspace,
        string outputPath,
        string? projectName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates specific artifacts based on the request.
    /// </summary>
    /// <param name="request">The generation request specifying what to generate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the generation operation.</returns>
    Task<GenerationResult> GenerateArtifactsAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a SignalR mock fixture.
    /// </summary>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the generation operation.</returns>
    Task<GenerationResult> GenerateSignalRMockAsync(
        string outputPath,
        CancellationToken cancellationToken = default);
}
