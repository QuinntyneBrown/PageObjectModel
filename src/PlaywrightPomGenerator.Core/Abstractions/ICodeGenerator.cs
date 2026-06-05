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

    /// <summary>
    /// Generates Playwright Component Object Model classes for the components in a project.
    /// Each non-page component becomes a root-<c>Locator</c>-scoped component object suitable for
    /// composition inside page objects.
    /// </summary>
    /// <param name="project">The Angular project information.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="excludeRoutable">When true, components whose <see cref="AngularComponentInfo.IsRoutable"/> is true are skipped.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the generation operation.</returns>
    Task<GenerationResult> GenerateComponentObjectsAsync(
        AngularProjectInfo project,
        string outputPath,
        bool excludeRoutable = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the interface mock harness: the runtime registry, per-interface recording mocks, the
    /// Angular providers that wire each token to its mock, and the Playwright-side typed client.
    /// </summary>
    /// <param name="interfaces">The discovered injection-token interfaces.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the generation operation.</returns>
    Task<GenerationResult> GenerateInterfaceMocksAsync(
        IReadOnlyList<InjectionTokenInterface> interfaces,
        string outputPath,
        CancellationToken cancellationToken = default);
}
