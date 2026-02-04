namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents the result of a code generation operation.
/// </summary>
public sealed record GenerationResult
{
    /// <summary>
    /// Gets whether the generation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the collection of generated files.
    /// </summary>
    public IReadOnlyList<GeneratedFile> GeneratedFiles { get; init; } = [];

    /// <summary>
    /// Gets the collection of errors encountered during generation.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets the collection of warnings encountered during generation.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful result with the generated files.
    /// </summary>
    /// <param name="files">The generated files.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A successful generation result.</returns>
    public static GenerationResult Successful(
        IReadOnlyList<GeneratedFile> files,
        IReadOnlyList<string>? warnings = null) =>
        new()
        {
            Success = true,
            GeneratedFiles = files,
            Warnings = warnings ?? []
        };

    /// <summary>
    /// Creates a failed result with the specified errors.
    /// </summary>
    /// <param name="errors">The errors that caused the failure.</param>
    /// <returns>A failed generation result.</returns>
    public static GenerationResult Failed(IReadOnlyList<string> errors) =>
        new()
        {
            Success = false,
            Errors = errors
        };

    /// <summary>
    /// Creates a failed result with a single error.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <returns>A failed generation result.</returns>
    public static GenerationResult Failed(string error) =>
        Failed([error]);
}

/// <summary>
/// Represents a generated file.
/// </summary>
public sealed record GeneratedFile
{
    /// <summary>
    /// Gets the relative path of the generated file.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Gets the absolute path of the generated file.
    /// </summary>
    public required string AbsolutePath { get; init; }

    /// <summary>
    /// Gets the type of the generated file.
    /// </summary>
    public required GeneratedFileType FileType { get; init; }

    /// <summary>
    /// Gets the file content.
    /// </summary>
    public required string Content { get; init; }
}

/// <summary>
/// Defines the type of generated file.
/// </summary>
public enum GeneratedFileType
{
    /// <summary>
    /// Page object file.
    /// </summary>
    PageObject,

    /// <summary>
    /// Test fixture file.
    /// </summary>
    Fixture,

    /// <summary>
    /// Playwright configuration file.
    /// </summary>
    Config,

    /// <summary>
    /// Selectors file.
    /// </summary>
    Selectors,

    /// <summary>
    /// Helper utilities file.
    /// </summary>
    Helper,

    /// <summary>
    /// Test specification file.
    /// </summary>
    TestSpec,

    /// <summary>
    /// SignalR mock file.
    /// </summary>
    SignalRMock
}
