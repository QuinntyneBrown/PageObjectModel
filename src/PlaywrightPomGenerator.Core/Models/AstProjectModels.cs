namespace PlaywrightPomGenerator.Core.Models;

// Wire DTOs mirroring the sidecar's analyzeProject JSON response one-to-one
// (schemaVersion 1 — a frozen contract; additive changes only). Deserialized
// with camelCase naming; no analysis or naming logic lives here.

/// <summary>
/// A project to analyze, passed to the sidecar.
/// </summary>
/// <param name="Name">The project name.</param>
/// <param name="SourceRoot">The absolute source root to scan.</param>
/// <param name="Prefix">The component selector prefix, when known.</param>
public sealed record AstProjectTarget(string Name, string SourceRoot, string? Prefix);

/// <summary>
/// Top-level analyzeProject response.
/// </summary>
public sealed record AstProjectAnalysis
{
    public int SchemaVersion { get; init; }
    public AstEngineInfo? Engine { get; init; }
    public IReadOnlyList<AstProjectResult> Projects { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Versions of the toolchain the sidecar resolved.
/// </summary>
public sealed record AstEngineInfo
{
    public string? Node { get; init; }
    public string? TypeScript { get; init; }
    public string? AngularCompiler { get; init; }
    public string? CompilerSource { get; init; }
}

/// <summary>
/// Analysis result for one project.
/// </summary>
public sealed record AstProjectResult
{
    public string Name { get; init; } = "";
    public IReadOnlyList<AstComponent> Components { get; init; } = [];
    public AstRouteAnalysis? Routes { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// One @Component class with its template analysis.
/// </summary>
public sealed record AstComponent
{
    public string ClassName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string? Selector { get; init; }
    public IReadOnlyList<string> Selectors { get; init; } = [];
    public bool? Standalone { get; init; }
    public string? TemplateSource { get; init; }
    public string? TemplateUrl { get; init; }
    public bool TemplateParsed { get; init; }
    public IReadOnlyList<string> TemplateErrors { get; init; } = [];
    public IReadOnlyList<AstPort> Inputs { get; init; } = [];
    public IReadOnlyList<AstPort> Outputs { get; init; } = [];
    public IReadOnlyList<AstQuery> Queries { get; init; } = [];
    public IReadOnlyList<string> ImportsIdentifiers { get; init; } = [];
    public IReadOnlyList<AstDialogOpen> DialogOpens { get; init; } = [];
    public AstTemplate? Template { get; init; }
    public string? TemplateContent { get; init; }
    public IReadOnlyList<AstChildComponent> ChildComponents { get; init; } = [];
}

/// <summary>An input/output port.</summary>
public sealed record AstPort
{
    public string Name { get; init; } = "";
    public string? Kind { get; init; }
    public string? Alias { get; init; }
    public bool Required { get; init; }
}

/// <summary>A view query (viewChild / viewChildren).</summary>
public sealed record AstQuery
{
    public string Name { get; init; } = "";
    public string? Kind { get; init; }
    public bool Signal { get; init; }
    public string? Target { get; init; }
}

/// <summary>A MatDialog.open(X) call site.</summary>
public sealed record AstDialogOpen
{
    public string? Handler { get; init; }
    public string? ComponentName { get; init; }
}

/// <summary>Template analysis output for one component.</summary>
public sealed record AstTemplate
{
    public IReadOnlyList<AstElement> Elements { get; init; } = [];
    public IReadOnlyList<AstForm> Forms { get; init; } = [];
}

/// <summary>One element of interest in a template.</summary>
public sealed record AstElement
{
    public string Tag { get; init; } = "";
    public string? TestId { get; init; }
    public string? Id { get; init; }
    public IReadOnlyList<string> Classes { get; init; } = [];
    public string? Role { get; init; }
    public AstAria Aria { get; init; } = new();
    public AstLabels Labels { get; init; } = new();
    public AstText Text { get; init; } = new();
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Events { get; init; } = [];
    public IReadOnlyDictionary<string, string> Handlers { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> TwoWay { get; init; } = [];
    public AstFormFacts Form { get; init; } = new();
    public AstStructure Structure { get; init; } = new();
    public AstAncestry Ancestry { get; init; } = new();
    public string? Widget { get; init; }
    public bool IsRouterLink { get; init; }
    public string? RouterLinkValue { get; init; }
    public AstTable? Table { get; init; }
    public bool HasNgContent { get; init; }
    public int? Line { get; init; }
}

/// <summary>ARIA facts of an element.</summary>
public sealed record AstAria
{
    public string? Label { get; init; }
    public string? LabelledBy { get; init; }
    public string? LabelledByText { get; init; }
    public string? DescribedBy { get; init; }
}

/// <summary>Label association facts of an element.</summary>
public sealed record AstLabels
{
    public string? WrappingLabel { get; init; }
    public string? LabelFor { get; init; }
    public string? MatLabel { get; init; }
    public string? Placeholder { get; init; }
}

/// <summary>Direct text facts of an element.</summary>
public sealed record AstText
{
    public string? Value { get; init; }
    public bool Interpolated { get; init; }
}

/// <summary>Reactive/template form facts of an element.</summary>
public sealed record AstFormFacts
{
    public string? FormControlName { get; init; }
    public string? FormGroupName { get; init; }
    public string? FormGroup { get; init; }
    public string? NgModel { get; init; }
    public string? InputType { get; init; }
}

/// <summary>Structural (control-flow) context of an element.</summary>
public sealed record AstStructure
{
    public bool Conditional { get; init; }
    public string? Condition { get; init; }
    public bool Repeated { get; init; }
    public string? RepeatAlias { get; init; }
    public string? TrackBy { get; init; }
    public string? SwitchCase { get; init; }
    public bool Projected { get; init; }
    public int Depth { get; init; }
}

/// <summary>Landmark/heading ancestry of an element.</summary>
public sealed record AstAncestry
{
    public AstLandmark? Landmark { get; init; }
    public string? HeadingText { get; init; }
}

/// <summary>A landmark ancestor.</summary>
public sealed record AstLandmark
{
    public string? Label { get; init; }
    public string? TestId { get; init; }
    public string? Role { get; init; }
    public string? AccessibleName { get; init; }
    public string? SelectorValue { get; init; }
}

/// <summary>Table facts of an element.</summary>
public sealed record AstTable
{
    public bool IsTable { get; init; }
    public bool IsMatTable { get; init; }
    public IReadOnlyList<AstTableColumn> Columns { get; init; } = [];
}

/// <summary>One matColumnDef column.</summary>
public sealed record AstTableColumn
{
    public string Id { get; init; } = "";
    public string? HeaderText { get; init; }
}

/// <summary>A reactive form found in a template.</summary>
public sealed record AstForm
{
    public string? FormGroup { get; init; }
    public string? SubmitHandler { get; init; }
    public IReadOnlyList<AstFormControl> Controls { get; init; } = [];
}

/// <summary>One control of a discovered form.</summary>
public sealed record AstFormControl
{
    public string Name { get; init; } = "";
    public string? InputType { get; init; }
    public string? Widget { get; init; }
    public string? Tag { get; init; }
}

/// <summary>A child component reference found in a template.</summary>
public sealed record AstChildComponent
{
    public string Selector { get; init; } = "";
    public string? ComponentClassName { get; init; }
    public string? ComponentFilePath { get; init; }
    public int Count { get; init; }
    public bool Conditional { get; init; }
    public bool Repeated { get; init; }
    public string? Library { get; init; }
}

/// <summary>Route analysis for one project.</summary>
public sealed record AstRouteAnalysis
{
    public IReadOnlyList<AstRouteNode> Tree { get; init; } = [];
    public IReadOnlyList<AstComponentRoute> ComponentRoutes { get; init; } = [];
}

/// <summary>One node of the route tree.</summary>
public sealed record AstRouteNode
{
    public string? Path { get; init; }
    public string? FullPath { get; init; }
    public string? Component { get; init; }
    public string? ComponentFilePath { get; init; }
    public string? RedirectTo { get; init; }
    public string? PathMatch { get; init; }
    public string? Title { get; init; }
    public string? Outlet { get; init; }
    public IReadOnlyList<string> PathParams { get; init; } = [];
    public bool Wildcard { get; init; }
    public IReadOnlyList<string> Guards { get; init; } = [];
    public IReadOnlyList<string> DataKeys { get; init; } = [];
    public AstLoadTarget? LoadComponent { get; init; }
    public AstLoadTarget? LoadChildren { get; init; }
    public bool IsLazy { get; init; }
    public bool IsRoot { get; init; }
    public string? SourceFile { get; init; }
    public IReadOnlyList<AstRouteNode> Children { get; init; } = [];
}

/// <summary>A lazy loadComponent/loadChildren target.</summary>
public sealed record AstLoadTarget
{
    public string? Specifier { get; init; }
    public string? ExportName { get; init; }
    public string? ResolvedFile { get; init; }
    public bool Resolved { get; init; }
}

/// <summary>Flat component-to-route index.</summary>
public sealed record AstComponentRoute
{
    public string ComponentFilePath { get; init; } = "";
    public string ComponentClassName { get; init; } = "";
    public IReadOnlyList<string> FullPaths { get; init; } = [];
    public IReadOnlyList<string> Titles { get; init; } = [];
}
