using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate Playwright POM files for an Angular workspace.
/// </summary>
public sealed class GenerateWorkspaceCommand : Command
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
    /// Gets the project option.
    /// </summary>
    public Option<string?> ProjectOption { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateWorkspaceCommand"/> class.
    /// </summary>
    public GenerateWorkspaceCommand()
        : base("workspace", "Generate Playwright Page Object Model tests for an Angular workspace")
    {
        PathArgument = new Argument<string>("path")
        {
            Description = "Path to the Angular workspace"
        };

        OutputOption = new Option<string?>("-o", "--output")
        {
            Description = "Output directory for generated files"
        };

        ProjectOption = new Option<string?>("-p", "--project")
        {
            Description = "Specific project name to generate for (generates for all applications if not specified)"
        };

        Add(PathArgument);
        Add(OutputOption);
        Add(ProjectOption);
    }
}

/// <summary>
/// Handler for the generate workspace command.
/// </summary>
public sealed class GenerateWorkspaceCommandHandler
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateWorkspaceCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateWorkspaceCommandHandler"/> class.
    /// </summary>
    /// <param name="analyzer">The Angular analyzer.</param>
    /// <param name="generator">The code generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateWorkspaceCommandHandler(
        IAngularAnalyzer analyzer,
        ICodeGenerator generator,
        ILogger<GenerateWorkspaceCommandHandler> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="path">The path to the Angular workspace.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="project">The specific project name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(
        string path,
        string? output,
        string? project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        _logger.LogInformation("Analyzing Angular workspace at {Path}", path);

        if (!_analyzer.IsWorkspace(path))
        {
            _logger.LogError("The specified path is not a valid Angular workspace: {Path}", path);
            Console.Error.WriteLine($"Error: '{path}' is not a valid Angular workspace (no angular.json found)");
            return 1;
        }

        try
        {
            var workspace = await _analyzer.AnalyzeWorkspaceAsync(path, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Found {ProjectCount} projects in workspace",
                workspace.Projects.Count);

            var outputPath = output ?? path;

            var result = await _generator.GenerateForWorkspaceAsync(
                workspace, outputPath, project, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine($"Successfully generated {result.GeneratedFiles.Count} files");

                // Group files by project
                var filesByProject = result.GeneratedFiles
                    .GroupBy(f => GetProjectFromPath(f.RelativePath))
                    .OrderBy(g => g.Key);

                foreach (var group in filesByProject)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Project: {group.Key}");
                    foreach (var file in group)
                    {
                        Console.WriteLine($"  - {file.RelativePath}");
                    }
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
            _logger.LogError(ex, "Failed to generate files for workspace at {Path}", path);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string GetProjectFromPath(string relativePath)
    {
        var parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "unknown";
    }
}
