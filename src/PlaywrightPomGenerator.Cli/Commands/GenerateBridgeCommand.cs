using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate a Playwright bridge for InjectionToken-backed service interfaces.
/// </summary>
public sealed class GenerateBridgeCommand : Command
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
    /// Initializes a new instance of the <see cref="GenerateBridgeCommand"/> class.
    /// </summary>
    public GenerateBridgeCommand()
        : base("bridge", "Generate a Playwright bridge: window-exposed recording mocks for InjectionToken-backed service interfaces, so E2E tests can verify service calls, stub return values, and drive the UI.")
    {
        PathArgument = new Argument<string>("path")
        {
            Description = "Path to the Angular workspace, application, library, or folder to scan"
        };

        OutputOption = new Option<string?>(new[] { "-o", "--output" }, "Output directory for generated files");

        Add(PathArgument);
        Add(OutputOption);
    }
}

/// <summary>
/// Handler for the generate bridge command.
/// </summary>
public sealed class GenerateBridgeCommandHandler
{
    private readonly ITypeScriptAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateBridgeCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateBridgeCommandHandler"/> class.
    /// </summary>
    /// <param name="analyzer">The TypeScript analyzer (sidecar-backed).</param>
    /// <param name="generator">The code generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateBridgeCommandHandler(
        ITypeScriptAnalyzer analyzer,
        ICodeGenerator generator,
        ILogger<GenerateBridgeCommandHandler> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="path">The path to scan.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(string path, string? output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        _logger.LogInformation("Scanning for InjectionToken interfaces at {Path}", path);

        try
        {
            var interfaces = await _analyzer.DiscoverInjectionTokenInterfacesAsync(path, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Found {Count} InjectionToken interface(s)", interfaces.Count);

            if (interfaces.Count == 0)
            {
                _logger.LogError("No InjectionToken-backed service interfaces found at {Path}", path);
                Console.Error.WriteLine($"Error: No InjectionToken-backed service interfaces found at '{path}'");
                return 1;
            }

            var outputPath = output ?? Path.Combine(path, "e2e");

            var result = await _generator.GenerateBridgeAsync(interfaces, outputPath, cancellationToken)
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
            _logger.LogError(ex, "Failed to generate bridge for {Path}", path);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
