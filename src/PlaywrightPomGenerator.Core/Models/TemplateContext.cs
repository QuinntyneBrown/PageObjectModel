namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Generation-time context built by the code generator (never by the analyzer):
/// what else is being generated in the same run, so templates only emit
/// references that resolve.
/// </summary>
public sealed record TemplateContext
{
    /// <summary>
    /// Gets an empty context (all context-gated features off).
    /// </summary>
    public static readonly TemplateContext Empty = new();

    /// <summary>
    /// Gets the component class names that get a component object emitted in
    /// this run. Composition accessors are only generated for these.
    /// </summary>
    public IReadOnlySet<string> ComponentObjectNames { get; init; } = new HashSet<string>();

    /// <summary>
    /// Gets the best host-page URL per embedded component name, derived from
    /// the route tree (drives HOST_PAGE_URL in component test specs).
    /// </summary>
    public IReadOnlyDictionary<string, string> HostPageUrls { get; init; } = new Dictionary<string, string>();
}
