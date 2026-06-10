using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate Playwright Component Object Model classes for Angular components.
/// </summary>
public sealed class GenerateComponentCommand : Command
{
    /// <summary>
    /// Gets the path argument.
    /// </summary>
    public Argument<string> PathArgument { get; }

    /// <summary>
    /// Gets the output option.
    /// </summary>
    public Option<string?> OutputOption { get; }

    /// <summary>
    /// Gets the exclude-routable option.
    /// </summary>
    public Option<bool> ExcludeRoutableOption { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateComponentCommand"/> class.
    /// </summary>
    public GenerateComponentCommand()
        : base("component", "Generate Playwright Component Object Model classes for Angular components (root-scoped, for composition inside pages).")
    {
        PathArgument = new Argument<string>("path")
        {
            Description = "Path to the Angular application, library, or component folder"
        };

        OutputOption = new Option<string?>(new[] { "-o", "--output" }, "Output directory for generated files");

        ExcludeRoutableOption = new Option<bool>(
            "--exclude-routable",
            "Skip components that are routable pages. By default, component objects are generated for every component discovered at the path.");

        Add(PathArgument);
        Add(OutputOption);
        Add(ExcludeRoutableOption);
    }
}

/// <summary>
/// Handler for the generate component command.
/// </summary>
public sealed class GenerateComponentCommandHandler
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateComponentCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateComponentCommandHandler"/> class.
    /// </summary>
    /// <param name="analyzer">The Angular analyzer.</param>
    /// <param name="generator">The code generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateComponentCommandHandler(
        IAngularAnalyzer analyzer,
        ICodeGenerator generator,
        ILogger<GenerateComponentCommandHandler> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="path">The path to the Angular application, library, or component folder.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="excludeRoutable">When true, routable components are skipped.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(string path, string? output, bool excludeRoutable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        _logger.LogInformation("Analyzing Angular components at {Path}", path);

        try
        {
            var projectName = GetProjectName(path);
            var project = await _analyzer.AnalyzeComponentsAtPathAsync(path, projectName, cancellationToken)
                .ConfigureAwait(false);

            ResultPrinter.PrintAnalysisEngine(project.Analysis);

            _logger.LogInformation(
                "Found {ComponentCount} component(s) at {Path}",
                project.Components.Count, path);

            if (project.Components.Count == 0)
            {
                _logger.LogError("No Angular components found at {Path}", path);
                Console.Error.WriteLine($"Error: No Angular components found at '{path}'");
                return 1;
            }

            var outputPath = output ?? Path.Combine(path, "e2e");

            var result = await _generator.GenerateComponentObjectsAsync(project, outputPath, excludeRoutable, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine($"Successfully generated {result.GeneratedFiles.Count} files in {outputPath}");

                foreach (var file in result.GeneratedFiles)
                {
                    Console.WriteLine($"  - {file.RelativePath}");
                }

                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Warnings:");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"  - {warning}");
                    }
                }

                return 0;
            }

            Console.Error.WriteLine("Generation failed:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate component objects for path {Path}", path);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string GetProjectName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? "components" : name;
    }
}
