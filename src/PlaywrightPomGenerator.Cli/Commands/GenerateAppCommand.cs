using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate Playwright POM files for a single Angular application.
/// </summary>
public sealed class GenerateAppCommand : Command
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
    /// Initializes a new instance of the <see cref="GenerateAppCommand"/> class.
    /// </summary>
    public GenerateAppCommand()
        : base("app", "Generate Playwright Page Object Model tests for a single Angular application")
    {
        PathArgument = new Argument<string>("path")
        {
            Description = "Path to the Angular application"
        };

        OutputOption = new Option<string?>(new[] { "-o", "--output" }, "Output directory for generated files");

        Add(PathArgument);
        Add(OutputOption);
    }
}

/// <summary>
/// Handler for the generate app command.
/// </summary>
public sealed class GenerateAppCommandHandler
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateAppCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateAppCommandHandler"/> class.
    /// </summary>
    /// <param name="analyzer">The Angular analyzer.</param>
    /// <param name="generator">The code generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateAppCommandHandler(
        IAngularAnalyzer analyzer,
        ICodeGenerator generator,
        ILogger<GenerateAppCommandHandler> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="path">The path to the Angular application.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(string path, string? output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        _logger.LogInformation("Analyzing Angular application at {Path}", path);

        if (!_analyzer.IsApplication(path))
        {
            _logger.LogError("The specified path is not a valid Angular application: {Path}", path);
            Console.Error.WriteLine($"Error: '{path}' is not a valid Angular application");
            return 1;
        }

        try
        {
            var project = await _analyzer.AnalyzeApplicationAsync(path, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Found {ComponentCount} components in {ProjectName}",
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
            _logger.LogError(ex, "Failed to generate files for application at {Path}", path);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
