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
    /// Gets the template content (HTML) for debugging purposes.
    /// </summary>
    public string? TemplateContent { get; init; }

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
    /// Gets the route path if this component is routed. With route evidence this
    /// is the primary full path (shortest parameter-free, else the first found),
    /// e.g. "users/:userId".
    /// </summary>
    public string? RoutePath { get; init; }

    /// <summary>
    /// Gets whether this component is a routable page (not a dialog, modal, button, etc.).
    /// </summary>
    public bool IsRoutable { get; init; }

    // --- Enrichment (populated by the AST analysis engine; defaults keep the
    // --- regex engine's output unchanged) ------------------------------------

    /// <summary>
    /// Gets whether the component is standalone (null when not declared; the
    /// effective default depends on the Angular version).
    /// </summary>
    public bool? IsStandalone { get; init; }

    /// <summary>
    /// Gets detailed input port information (decorator, signal, and model inputs).
    /// The legacy <see cref="Inputs"/> list stays populated for compatibility.
    /// </summary>
    public IReadOnlyList<ComponentPortInfo> InputsDetailed { get; init; } = [];

    /// <summary>
    /// Gets detailed output port information.
    /// </summary>
    public IReadOnlyList<ComponentPortInfo> OutputsDetailed { get; init; } = [];

    /// <summary>
    /// Gets the project components referenced in this component's template
    /// (drives page/component object composition).
    /// </summary>
    public IReadOnlyList<ChildComponentRef> ChildComponents { get; init; } = [];

    /// <summary>
    /// Gets the reactive forms discovered in the template.
    /// </summary>
    public IReadOnlyList<FormInfo> Forms { get; init; } = [];

    /// <summary>
    /// Gets every full route path that renders this component.
    /// </summary>
    public IReadOnlyList<string> RoutePaths { get; init; } = [];

    /// <summary>
    /// Gets the route parameter names of <see cref="RoutePath"/> (e.g. ["userId"]).
    /// </summary>
    public IReadOnlyList<string> RouteParams { get; init; } = [];

    /// <summary>
    /// Gets whether route analysis linked this component to a route
    /// (evidence-based routability, as opposed to the name/path heuristic).
    /// </summary>
    public bool RouteEvidence { get; init; }

    /// <summary>
    /// Gets the route title for this component, when declared.
    /// </summary>
    public string? TitleFromRoute { get; init; }
}

/// <summary>
/// A component input or output port.
/// </summary>
public sealed record ComponentPortInfo
{
    /// <summary>
    /// Gets the property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the binding alias, if declared.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Gets whether the port is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets the declaration kind.
    /// </summary>
    public ComponentPortKind Kind { get; init; } = ComponentPortKind.Decorator;
}

/// <summary>
/// How a component port is declared.
/// </summary>
public enum ComponentPortKind
{
    /// <summary>@Input() / @Output() decorator.</summary>
    Decorator,

    /// <summary>input() / output() signal function.</summary>
    Signal,

    /// <summary>model() two-way signal.</summary>
    Model
}

/// <summary>
/// A reference to a child component used in a template.
/// </summary>
public sealed record ChildComponentRef
{
    /// <summary>
    /// Gets the tag used in the template (e.g. "app-kpi-card").
    /// </summary>
    public required string Selector { get; init; }

    /// <summary>
    /// Gets the child component class name, when resolved to a project component.
    /// </summary>
    public string? ComponentName { get; init; }

    /// <summary>
    /// Gets the child component file path, when resolved.
    /// </summary>
    public string? ComponentFilePath { get; init; }

    /// <summary>
    /// Gets how many times the tag occurs in the template.
    /// </summary>
    public int Count { get; init; } = 1;

    /// <summary>
    /// Gets whether any occurrence renders conditionally.
    /// </summary>
    public bool IsConditional { get; init; }

    /// <summary>
    /// Gets whether any occurrence repeats.
    /// </summary>
    public bool IsRepeated { get; init; }

    /// <summary>
    /// Gets the owning UI library for known library tags (e.g. "@angular/material"),
    /// null for project components.
    /// </summary>
    public string? Library { get; init; }
}

/// <summary>
/// A reactive form discovered in a component template.
/// </summary>
public sealed record FormInfo
{
    /// <summary>
    /// Gets the formGroup expression / name, when present.
    /// </summary>
    public string? FormGroupName { get; init; }

    /// <summary>
    /// Gets the (ngSubmit) handler method name, when present.
    /// </summary>
    public string? SubmitHandlerName { get; init; }

    /// <summary>
    /// Gets the form controls in template order.
    /// </summary>
    public IReadOnlyList<FormControlInfo> Controls { get; init; } = [];
}

/// <summary>
/// A single control inside a discovered form.
/// </summary>
public sealed record FormControlInfo
{
    /// <summary>
    /// Gets the formControlName.
    /// </summary>
    public required string ControlName { get; init; }

    /// <summary>
    /// Gets the interaction kind of the control.
    /// </summary>
    public ControlType ControlType { get; init; } = ControlType.TextInput;

    /// <summary>
    /// Gets whether the control is required.
    /// </summary>
    public bool Required { get; init; }
}
