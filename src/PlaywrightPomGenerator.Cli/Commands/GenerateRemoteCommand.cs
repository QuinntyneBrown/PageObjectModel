using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate Playwright POM files from a remote git repository URL.
/// </summary>
public sealed class GenerateRemoteCommand : Command
{
    /// <summary>
    /// Gets the URL argument.
    /// </summary>
    public Argument<string> UrlArgument { get; }

    /// <summary>
    /// Gets the output option.
    /// </summary>
    public Option<string?> OutputOption { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateRemoteCommand"/> class.
    /// </summary>
    public GenerateRemoteCommand()
        : base("remote", "Generate Playwright Page Object Model tests from a remote git repository URL (GitHub, GitLab, Bitbucket, Azure DevOps, or any git host)")
    {
        UrlArgument = new Argument<string>("url")
        {
            Description = "Git URL to a component file, folder, or repository (e.g., https://github.com/owner/repo/blob/main/src/app/my-component/my-component.ts)"
        };

        OutputOption = new Option<string?>(new[] { "-o", "--output" },
            "Output directory for generated files. Defaults to the current working directory.");

        Add(UrlArgument);
        Add(OutputOption);
    }
}

/// <summary>
/// Handler for the generate remote command.
/// </summary>
public sealed class GenerateRemoteCommandHandler
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly IGitService _gitService;
    private readonly ILogger<GenerateRemoteCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateRemoteCommandHandler"/> class.
    /// </summary>
    /// <param name="analyzer">The Angular analyzer.</param>
    /// <param name="generator">The code generator.</param>
    /// <param name="gitService">The git service.</param>
    /// <param name="logger">The logger.</param>
    public GenerateRemoteCommandHandler(
        IAngularAnalyzer analyzer,
        ICodeGenerator generator,
        IGitService gitService,
        ILogger<GenerateRemoteCommandHandler> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="url">The git URL.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(string url, string? output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        // Verify git is available
        if (!await _gitService.IsGitAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            Console.Error.WriteLine("Error: git is not installed or not available on the system PATH.");
            Console.Error.WriteLine("Please install git and try again: https://git-scm.com/downloads");
            return 1;
        }

        // Parse the URL
        if (!GitUrlParser.TryParse(url, out var urlInfo) || urlInfo is null)
        {
            Console.Error.WriteLine($"Error: Unable to parse git URL: {url}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Supported URL formats:");
            Console.Error.WriteLine("  GitHub:      https://github.com/{owner}/{repo}/blob/{branch}/{path}");
            Console.Error.WriteLine("  GitLab:      https://gitlab.com/{owner}/{repo}/-/blob/{branch}/{path}");
            Console.Error.WriteLine("  Bitbucket:   https://bitbucket.org/{owner}/{repo}/src/{branch}/{path}");
            Console.Error.WriteLine("  Azure DevOps: https://dev.azure.com/{org}/{project}/_git/{repo}?path={path}");
            Console.Error.WriteLine("  Generic:     https://git.example.com/{owner}/{repo}.git");
            return 1;
        }

        _logger.LogInformation("Parsed URL - Provider: {Provider}, Repo: {Owner}/{Repo}, Ref: {Ref}, Path: {Path}",
            urlInfo.Provider, urlInfo.Owner, urlInfo.RepoName, urlInfo.Ref, urlInfo.PathInRepo);

        Console.WriteLine($"Repository: {urlInfo.Owner}/{urlInfo.RepoName}");
        if (!string.IsNullOrEmpty(urlInfo.Commit))
        {
            Console.WriteLine($"Commit: {urlInfo.Commit}");
        }
        else
        {
            Console.WriteLine($"Branch: {urlInfo.Branch ?? "(default)"}");
        }
        if (!string.IsNullOrEmpty(urlInfo.PathInRepo))
        {
            Console.WriteLine($"Path: {urlInfo.PathInRepo}");
        }

        string? tempPath = null;

        try
        {
            // Clone to temp directory
            Console.WriteLine();
            Console.WriteLine("Cloning repository...");
            tempPath = await _gitService.CloneToTempAsync(urlInfo, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("Clone complete.");

            // Resolve the target path within the cloned repo
            var targetPath = tempPath;
            if (!string.IsNullOrEmpty(urlInfo.PathInRepo))
            {
                targetPath = Path.Combine(tempPath, urlInfo.PathInRepo.Replace('/', Path.DirectorySeparatorChar));
            }

            // Determine the output path
            var outputPath = output ?? Path.Combine(Directory.GetCurrentDirectory(), "e2e");

            // Determine analysis strategy
            Console.WriteLine();
            Console.WriteLine("Analyzing Angular components...");

            var result = await AnalyzeAndGenerateAsync(
                tempPath, targetPath, urlInfo, outputPath, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine();
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
            _logger.LogError(ex, "Failed to generate files from remote URL {Url}", url);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            // Always clean up temp directory
            if (tempPath is not null)
            {
                Console.WriteLine();
                Console.WriteLine("Cleaning up temporary files...");
                _gitService.CleanupTempRepo(tempPath);
            }
        }
    }

    private async Task<Core.Models.GenerationResult> AnalyzeAndGenerateAsync(
        string repoRoot,
        string targetPath,
        Core.Models.GitUrlInfo urlInfo,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var projectName = urlInfo.RepoName ?? "remote";

        // Strategy 1: Check if the target path (or repo root) is a full Angular app/workspace
        if (_analyzer.IsWorkspace(targetPath) || _analyzer.IsApplication(targetPath))
        {
            _logger.LogInformation("Detected Angular application/workspace at {Path}", targetPath);
            Console.WriteLine("Detected Angular application/workspace.");

            var project = await _analyzer.AnalyzeApplicationAsync(targetPath, cancellationToken)
                .ConfigureAwait(false);

            Console.WriteLine($"Found {project.Components.Count} components in {project.Name}.");

            return await _generator.GenerateForApplicationAsync(project, outputPath, cancellationToken)
                .ConfigureAwait(false);
        }

        // Strategy 2: Check if it's a library
        if (_analyzer.IsLibrary(targetPath))
        {
            _logger.LogInformation("Detected Angular library at {Path}", targetPath);
            Console.WriteLine("Detected Angular library.");

            var project = await _analyzer.AnalyzeLibraryAsync(targetPath, cancellationToken)
                .ConfigureAwait(false);

            Console.WriteLine($"Found {project.Components.Count} components in {project.Name}.");

            return await _generator.GenerateForApplicationAsync(project, outputPath, cancellationToken)
                .ConfigureAwait(false);
        }

        // Strategy 3: Walk up from target to find angular.json in the repo
        var angularRoot = FindAngularRoot(targetPath, repoRoot);
        if (angularRoot is not null)
        {
            _logger.LogInformation("Found Angular root at {Path}", angularRoot);
            Console.WriteLine($"Found Angular project root at: {Path.GetRelativePath(repoRoot, angularRoot)}");

            if (_analyzer.IsWorkspace(angularRoot))
            {
                var workspace = await _analyzer.AnalyzeWorkspaceAsync(angularRoot, cancellationToken)
                    .ConfigureAwait(false);

                // Try to find the project that contains the target path
                var matchingProject = workspace.Projects
                    .FirstOrDefault(p => targetPath.StartsWith(p.RootPath, StringComparison.OrdinalIgnoreCase)
                                      || targetPath.StartsWith(p.SourceRoot, StringComparison.OrdinalIgnoreCase));

                if (matchingProject is not null)
                {
                    Console.WriteLine($"Found {matchingProject.Components.Count} components in {matchingProject.Name}.");
                    return await _generator.GenerateForApplicationAsync(matchingProject, outputPath, cancellationToken)
                        .ConfigureAwait(false);
                }

                // Fall back to first application project
                var appProject = workspace.Projects
                    .FirstOrDefault(p => p.ProjectType == Core.Models.AngularProjectType.Application)
                    ?? workspace.Projects.FirstOrDefault();

                if (appProject is not null)
                {
                    Console.WriteLine($"Found {appProject.Components.Count} components in {appProject.Name}.");
                    return await _generator.GenerateForApplicationAsync(appProject, outputPath, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                var project = await _analyzer.AnalyzeApplicationAsync(angularRoot, cancellationToken)
                    .ConfigureAwait(false);

                Console.WriteLine($"Found {project.Components.Count} components in {project.Name}.");

                return await _generator.GenerateForApplicationAsync(project, outputPath, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Strategy 4: Analyze components directly at the target path (file or folder)
        _logger.LogInformation("No Angular project root found. Analyzing components directly at {Path}", targetPath);
        Console.WriteLine("No Angular project root found. Scanning for components directly...");

        var directProject = await _analyzer.AnalyzeComponentsAtPathAsync(
            targetPath, projectName, cancellationToken)
            .ConfigureAwait(false);

        if (directProject.Components.Count == 0)
        {
            return Core.Models.GenerationResult.Failed(
                $"No Angular components found at the specified path: {urlInfo.PathInRepo ?? "/"}");
        }

        Console.WriteLine($"Found {directProject.Components.Count} component(s).");

        return await _generator.GenerateForApplicationAsync(directProject, outputPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? FindAngularRoot(string startPath, string stopAtPath)
    {
        var current = Directory.Exists(startPath) ? startPath : Path.GetDirectoryName(startPath);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "angular.json")))
            {
                return current;
            }

            if (File.Exists(Path.Combine(current, "package.json")))
            {
                // Check if package.json references @angular/core
                try
                {
                    var content = File.ReadAllText(Path.Combine(current, "package.json"));
                    if (content.Contains("@angular/core"))
                    {
                        return current;
                    }
                }
                catch
                {
                    // Ignore read errors
                }
            }

            // Don't go above the repo root
            if (string.Equals(current, stopAtPath, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }
}
