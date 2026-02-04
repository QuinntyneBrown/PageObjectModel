using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate Playwright POM files for a single Angular library.
/// </summary>
public sealed class GenerateLibraryCommand : Command
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
    /// Initializes a new instance of the <see cref="GenerateLibraryCommand"/> class.
    /// </summary>
    public GenerateLibraryCommand()
        : base("lib", "Generate Playwright Page Object Model tests for a single Angular library")
    {
        PathArgument = new Argument<string>("path")
        {
            Description = "Path to the Angular library"
        };

        OutputOption = new Option<string?>(new[] { "-o", "--output" }, "Output directory for generated files");

        Add(PathArgument);
        Add(OutputOption);
    }
}

/// <summary>
/// Handler for the generate library command.
/// </summary>
public sealed class GenerateLibraryCommandHandler
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateLibraryCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateLibraryCommandHandler"/> class.
    /// </summary>
    /// <param name="analyzer">The Angular analyzer.</param>
    /// <param name="generator">The code generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateLibraryCommandHandler(
        IAngularAnalyzer analyzer,
        ICodeGenerator generator,
        ILogger<GenerateLibraryCommandHandler> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="path">The path to the Angular library.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(string path, string? output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        _logger.LogInformation("Analyzing Angular library at {Path}", path);

        // Check if it's a library directly or a workspace containing libraries
        if (!_analyzer.IsLibrary(path) && !_analyzer.IsWorkspace(path))
        {
            _logger.LogError("The specified path is not a valid Angular library: {Path}", path);
            Console.Error.WriteLine($"Error: '{path}' is not a valid Angular library (no ng-package.json found)");
            return 1;
        }

        try
        {
            var project = await _analyzer.AnalyzeLibraryAsync(path, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Found {ComponentCount} components in library {ProjectName}",
                project.Components.Count, project.Name);

            var outputPath = output ?? Path.Combine(path, "e2e");

            var result = await _generator.GenerateForApplicationAsync(project, outputPath, cancellationToken)
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
            _logger.LogError(ex, "Failed to generate files for library at {Path}", path);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
