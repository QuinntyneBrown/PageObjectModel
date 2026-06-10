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

    /// <summary>
    /// Gets the workspace package facts (package.json / angular.json), when inspected.
    /// </summary>
    public PackageReport? Packages { get; init; }

    /// <summary>
    /// Gets the analysis engine report for this project, when available.
    /// </summary>
    public AnalysisReport? Analysis { get; init; }

    /// <summary>
    /// Gets the dist output facts, when dist analysis ran.
    /// </summary>
    public DistAnalysis? Dist { get; init; }
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

    // --- Enrichment (populated by the AST analysis engine) -------------------

    /// <summary>
    /// Gets the full path from the route root (e.g. "/orders/:id").
    /// </summary>
    public string? FullPath { get; init; }

    /// <summary>
    /// Gets the resolved component file path, when linked.
    /// </summary>
    public string? ComponentFilePath { get; init; }

    /// <summary>
    /// Gets the route parameter names in <see cref="FullPath"/>.
    /// </summary>
    public IReadOnlyList<string> PathParameters { get; init; } = [];

    /// <summary>
    /// Gets whether the route is the ** wildcard.
    /// </summary>
    public bool IsWildcard { get; init; }

    /// <summary>
    /// Gets the pathMatch value, when declared.
    /// </summary>
    public string? PathMatch { get; init; }

    /// <summary>
    /// Gets the route title, when declared.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the named outlet, when declared.
    /// </summary>
    public string? Outlet { get; init; }

    /// <summary>
    /// Gets the guard identifier names (canActivate, canMatch, ...).
    /// </summary>
    public IReadOnlyList<string> Guards { get; init; } = [];

    /// <summary>
    /// Gets the literal keys of the route data object.
    /// </summary>
    public IReadOnlyList<string> DataKeys { get; init; } = [];

    /// <summary>
    /// Gets the file the route was declared in.
    /// </summary>
    public string? SourceFilePath { get; init; }
}
