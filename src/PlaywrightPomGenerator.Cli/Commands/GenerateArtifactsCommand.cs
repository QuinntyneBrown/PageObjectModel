using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate specific artifacts for an Angular workspace or application.
/// </summary>
public sealed class GenerateArtifactsCommand : Command
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
    /// Gets the fixtures option.
    /// </summary>
    public Option<bool> FixturesOption { get; }

    /// <summary>
    /// Gets the configs option.
    /// </summary>
    public Option<bool> ConfigsOption { get; }

    /// <summary>
    /// Gets the selectors option.
    /// </summary>
    public Option<bool> SelectorsOption { get; }

    /// <summary>
    /// Gets the page objects option.
    /// </summary>
    public Option<bool> PageObjectsOption { get; }

    /// <summary>
    /// Gets the helpers option.
    /// </summary>
    public Option<bool> HelpersOption { get; }

    /// <summary>
    /// Gets the all option.
    /// </summary>
    public Option<bool> AllOption { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateArtifactsCommand"/> class.
    /// </summary>
    public GenerateArtifactsCommand()
        : base("artifacts", "Generate specific Playwright artifacts (fixtures, configs, selectors, page objects, helpers)")
    {
        PathArgument = new Argument<string>("path")
        {
            Description = "Path to the Angular workspace or application"
        };

        OutputOption = new Option<string?>("-o", "--output")
        {
            Description = "Output directory for generated files"
        };

        ProjectOption = new Option<string?>("-p", "--project")
        {
            Description = "Specific project name (for workspaces)"
        };

        FixturesOption = new Option<bool>("-f", "--fixtures")
        {
            Description = "Generate test fixtures"
        };

        ConfigsOption = new Option<bool>("-c", "--configs")
        {
            Description = "Generate Playwright configuration"
        };

        SelectorsOption = new Option<bool>("-s", "--selectors")
        {
            Description = "Generate selector files"
        };

        PageObjectsOption = new Option<bool>("--page-objects")
        {
            Description = "Generate page object files"
        };

        HelpersOption = new Option<bool>("--helpers")
        {
            Description = "Generate helper utilities"
        };

        AllOption = new Option<bool>("-a", "--all")
        {
            Description = "Generate all artifacts"
        };

        Add(PathArgument);
        Add(OutputOption);
        Add(ProjectOption);
        Add(FixturesOption);
        Add(ConfigsOption);
        Add(SelectorsOption);
        Add(PageObjectsOption);
        Add(HelpersOption);
        Add(AllOption);
    }
}

/// <summary>
/// Handler for the generate artifacts command.
/// </summary>
public sealed class GenerateArtifactsCommandHandler
{
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateArtifactsCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateArtifactsCommandHandler"/> class.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateArtifactsCommandHandler(
        ICodeGenerator generator,
        ILogger<GenerateArtifactsCommandHandler> logger)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="path">The path to the Angular workspace or application.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="project">The specific project name.</param>
    /// <param name="fixtures">Generate fixtures.</param>
    /// <param name="configs">Generate configs.</param>
    /// <param name="selectors">Generate selectors.</param>
    /// <param name="pageObjects">Generate page objects.</param>
    /// <param name="helpers">Generate helpers.</param>
    /// <param name="all">Generate all artifacts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(
        string path,
        string? output,
        string? project,
        bool fixtures,
        bool configs,
        bool selectors,
        bool pageObjects,
        bool helpers,
        bool all,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        // If 'all' is specified, enable all options
        if (all)
        {
            fixtures = true;
            configs = true;
            selectors = true;
            pageObjects = true;
            helpers = true;
        }

        // Validate at least one option is selected
        if (!fixtures && !configs && !selectors && !pageObjects && !helpers)
        {
            Console.Error.WriteLine("Error: At least one artifact type must be specified.");
            Console.Error.WriteLine("Use --all to generate all artifacts, or specify individual options:");
            Console.Error.WriteLine("  --fixtures, --configs, --selectors, --page-objects, --helpers");
            return 1;
        }

        _logger.LogInformation("Generating artifacts for {Path}", path);

        var request = new GenerationRequest
        {
            TargetPath = path,
            OutputPath = output,
            ProjectName = project,
            GenerateFixtures = fixtures,
            GenerateConfigs = configs,
            GenerateSelectors = selectors,
            GeneratePageObjects = pageObjects,
            GenerateHelpers = helpers
        };

        try
        {
            var result = await _generator.GenerateArtifactsAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine($"Successfully generated {result.GeneratedFiles.Count} files:");

                var filesByType = result.GeneratedFiles
                    .GroupBy(f => f.FileType)
                    .OrderBy(g => g.Key.ToString());

                foreach (var group in filesByType)
                {
                    Console.WriteLine();
                    Console.WriteLine($"{group.Key}:");
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
            _logger.LogError(ex, "Failed to generate artifacts for {Path}", path);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
