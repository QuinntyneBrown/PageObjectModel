namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Facts read from the analyzed workspace's package.json and angular.json that
/// inform analysis and generation (UI library awareness, component prefixes,
/// node_modules availability).
/// </summary>
public sealed record PackageReport
{
    /// <summary>
    /// Gets an empty report (no package.json found).
    /// </summary>
    public static readonly PackageReport Empty = new();

    /// <summary>
    /// Gets the @angular/core version string, if declared.
    /// </summary>
    public string? AngularCoreVersion { get; init; }

    /// <summary>
    /// Gets the @angular/material version string, if declared.
    /// </summary>
    public string? AngularMaterialVersion { get; init; }

    /// <summary>
    /// Gets the @angular/cdk version string, if declared.
    /// </summary>
    public string? AngularCdkVersion { get; init; }

    /// <summary>
    /// Gets the typescript version string, if declared.
    /// </summary>
    public string? TypeScriptVersion { get; init; }

    /// <summary>
    /// Gets other recognized UI libraries (name to declared version).
    /// </summary>
    public IReadOnlyDictionary<string, string> UiLibraries { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the component selector prefixes from angular.json (workspace-level
    /// schematics prefix plus per-project prefixes).
    /// </summary>
    public IReadOnlyList<string> Prefixes { get; init; } = [];

    /// <summary>
    /// Gets whether a node_modules directory exists next to package.json
    /// (predicts whether the AST sidecar can resolve typescript).
    /// </summary>
    public bool NodeModulesPresent { get; init; }

    /// <summary>
    /// Gets the path of the package.json that was read, if any.
    /// </summary>
    public string? PackageJsonPath { get; init; }

    /// <summary>
    /// Gets whether @angular/material is declared.
    /// </summary>
    public bool HasMaterial => !string.IsNullOrEmpty(AngularMaterialVersion);
}
