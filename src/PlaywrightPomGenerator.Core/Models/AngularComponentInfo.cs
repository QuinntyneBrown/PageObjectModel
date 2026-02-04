namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents information about an Angular component discovered during analysis.
/// </summary>
public sealed record AngularComponentInfo
{
    /// <summary>
    /// Gets the component name (e.g., "AppComponent", "LoginComponent").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the component selector (e.g., "app-root", "app-login").
    /// </summary>
    public required string Selector { get; init; }

    /// <summary>
    /// Gets the path to the component TypeScript file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the path to the component template file (HTML).
    /// </summary>
    public string? TemplatePath { get; init; }

    /// <summary>
    /// Gets the collection of selectable elements found in the template.
    /// </summary>
    public IReadOnlyList<ElementSelector> Selectors { get; init; } = [];

    /// <summary>
    /// Gets the collection of input properties.
    /// </summary>
    public IReadOnlyList<string> Inputs { get; init; } = [];

    /// <summary>
    /// Gets the collection of output events.
    /// </summary>
    public IReadOnlyList<string> Outputs { get; init; } = [];

    /// <summary>
    /// Gets the route path if this component is routed.
    /// </summary>
    public string? RoutePath { get; init; }
}
