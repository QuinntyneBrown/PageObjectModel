namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents information about an Angular workspace (angular.json).
/// </summary>
public sealed record AngularWorkspaceInfo
{
    /// <summary>
    /// Gets the workspace root path.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Gets the Angular CLI version.
    /// </summary>
    public string? CliVersion { get; init; }

    /// <summary>
    /// Gets the default project name.
    /// </summary>
    public string? DefaultProject { get; init; }

    /// <summary>
    /// Gets the collection of projects in the workspace.
    /// </summary>
    public IReadOnlyList<AngularProjectInfo> Projects { get; init; } = [];
}
