namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Configuration options for the Playwright POM generator.
/// </summary>
public sealed class GeneratorOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Generator";

    /// <summary>
    /// Gets or sets the header template to be added to all generated files.
    /// Supports placeholders: {FileName}, {GeneratedDate}, {ToolVersion}.
    /// By default, no header is added. Set this to a non-empty string to include a header.
    /// </summary>
    public string FileHeader { get; set; } = "";

    /// <summary>
    /// Gets or sets the suffix for test files. Defaults to "spec".
    /// For example, with "spec" the test file will be named "component.spec.ts".
    /// With "test" it will be named "component.test.ts".
    /// </summary>
    public string TestFileSuffix { get; set; } = "spec";

    /// <summary>
    /// Gets or sets the tool version string used in generated file headers.
    /// </summary>
    public string ToolVersion { get; set; } = "1.9.0";

    /// <summary>
    /// Gets or sets the output directory name for generated test files.
    /// </summary>
    public string OutputDirectoryName { get; set; } = "e2e";

    /// <summary>
    /// Gets or sets whether to generate JSDoc comments on all methods and functions.
    /// </summary>
    public bool GenerateJsDocComments { get; set; } = true;

    /// <summary>
    /// Gets or sets the default timeout in milliseconds for Playwright operations.
    /// </summary>
    public int DefaultTimeout { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the base URL placeholder for generated tests.
    /// </summary>
    public string BaseUrlPlaceholder { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Gets or sets whether debug mode is enabled.
    /// When enabled, the HTML template is included as a comment in generated page object files.
    /// </summary>
    public bool DebugMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the analysis engine. Auto prefers AST analysis via the Node
    /// sidecar and falls back to regex analysis when unavailable.
    /// </summary>
    public AnalysisEngine AnalysisEngine { get; set; } = AnalysisEngine.Auto;

    /// <summary>
    /// Gets or sets whether app/workspace/remote generation also emits component
    /// objects (the components/ directory).
    /// </summary>
    public bool EmitComponentObjects { get; set; } = true;

    /// <summary>
    /// Gets or sets whether page objects embed typed accessors for the child
    /// component objects found in their templates.
    /// </summary>
    public bool EmitComposition { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the generated urls.config.ts includes the example
    /// API_ENDPOINTS block.
    /// </summary>
    public bool EmitApiEndpointsExample { get; set; } = false;

    /// <summary>
    /// Gets or sets the sidecar invocation timeout in seconds (0 disables the timeout).
    /// </summary>
    public int SidecarTimeoutSeconds { get; set; } = 600;
}
