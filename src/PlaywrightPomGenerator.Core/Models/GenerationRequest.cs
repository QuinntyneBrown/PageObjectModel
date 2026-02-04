namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents a request to generate specific artifacts.
/// </summary>
public sealed record GenerationRequest
{
    /// <summary>
    /// Gets the target path (workspace or application path).
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Gets the output path for generated files.
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// Gets whether to generate fixtures.
    /// </summary>
    public bool GenerateFixtures { get; init; }

    /// <summary>
    /// Gets whether to generate Playwright configuration.
    /// </summary>
    public bool GenerateConfigs { get; init; }

    /// <summary>
    /// Gets whether to generate selectors.
    /// </summary>
    public bool GenerateSelectors { get; init; }

    /// <summary>
    /// Gets whether to generate page objects.
    /// </summary>
    public bool GeneratePageObjects { get; init; }

    /// <summary>
    /// Gets whether to generate helper utilities.
    /// </summary>
    public bool GenerateHelpers { get; init; }

    /// <summary>
    /// Gets the specific project name to generate for (optional, for workspaces).
    /// </summary>
    public string? ProjectName { get; init; }

    /// <summary>
    /// Gets whether any generation option is enabled.
    /// </summary>
    public bool HasAnyGenerationOption =>
        GenerateFixtures || GenerateConfigs || GenerateSelectors ||
        GeneratePageObjects || GenerateHelpers;

    /// <summary>
    /// Creates a request to generate all artifacts.
    /// </summary>
    /// <param name="targetPath">The target path.</param>
    /// <param name="outputPath">Optional output path.</param>
    /// <returns>A generation request with all options enabled.</returns>
    public static GenerationRequest All(string targetPath, string? outputPath = null) =>
        new()
        {
            TargetPath = targetPath,
            OutputPath = outputPath,
            GenerateFixtures = true,
            GenerateConfigs = true,
            GenerateSelectors = true,
            GeneratePageObjects = true,
            GenerateHelpers = true
        };
}
