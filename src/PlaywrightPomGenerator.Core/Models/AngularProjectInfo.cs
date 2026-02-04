namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents information about an Angular project.
/// </summary>
public sealed record AngularProjectInfo
{
    /// <summary>
    /// Gets the project name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the project root path.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Gets the source root path (typically "src").
    /// </summary>
    public required string SourceRoot { get; init; }

    /// <summary>
    /// Gets the project type (application or library).
    /// </summary>
    public required AngularProjectType ProjectType { get; init; }

    /// <summary>
    /// Gets the collection of components discovered in this project.
    /// </summary>
    public IReadOnlyList<AngularComponentInfo> Components { get; init; } = [];

    /// <summary>
    /// Gets the route configuration if available.
    /// </summary>
    public IReadOnlyList<RouteInfo> Routes { get; init; } = [];
}

/// <summary>
/// Defines the type of Angular project.
/// </summary>
public enum AngularProjectType
{
    /// <summary>
    /// Standalone Angular application.
    /// </summary>
    Application,

    /// <summary>
    /// Angular library.
    /// </summary>
    Library
}

/// <summary>
/// Represents a route configuration entry.
/// </summary>
public sealed record RouteInfo
{
    /// <summary>
    /// Gets the route path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the component associated with this route.
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// Gets the redirect target if this is a redirect route.
    /// </summary>
    public string? RedirectTo { get; init; }

    /// <summary>
    /// Gets the child routes.
    /// </summary>
    public IReadOnlyList<RouteInfo> Children { get; init; } = [];

    /// <summary>
    /// Gets whether this route has lazy-loaded children.
    /// </summary>
    public bool IsLazyLoaded { get; init; }
}
