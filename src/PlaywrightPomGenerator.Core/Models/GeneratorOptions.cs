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
    public string ToolVersion { get; set; } = "1.5.0";

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
}
