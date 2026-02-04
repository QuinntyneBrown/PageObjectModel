using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Generates Playwright Page Object Model code for Angular applications.
/// </summary>
public sealed class CodeGenerator : ICodeGenerator
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ITemplateEngine _templateEngine;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CodeGenerator> _logger;
    private readonly GeneratorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGenerator"/> class.
    /// </summary>
    /// <param name="analyzer">The Angular analyzer.</param>
    /// <param name="templateEngine">The template engine.</param>
    /// <param name="fileSystem">The file system service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The generator options.</param>
    public CodeGenerator(
        IAngularAnalyzer analyzer,
        ITemplateEngine templateEngine,
        IFileSystem fileSystem,
        ILogger<CodeGenerator> logger,
        IOptions<GeneratorOptions> options)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateForApplicationAsync(
        AngularProjectInfo project,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(outputPath);

        _logger.LogInformation("Generating POM files for application {ProjectName}", project.Name);

        var generatedFiles = new List<GeneratedFile>();
        var warnings = new List<string>();

        try
        {
            var fullOutputPath = _fileSystem.GetFullPath(outputPath);
            _fileSystem.CreateDirectory(fullOutputPath);

            // Create directory structure
            var pagesDir = _fileSystem.CombinePath(fullOutputPath, "pages");
            var selectorsDir = _fileSystem.CombinePath(fullOutputPath, "selectors");
            var testsDir = _fileSystem.CombinePath(fullOutputPath, "tests");

            _fileSystem.CreateDirectory(pagesDir);
            _fileSystem.CreateDirectory(selectorsDir);
            _fileSystem.CreateDirectory(testsDir);

            // Deduplicate components by name (keep first occurrence, merge selectors from duplicates)
            var deduplicatedComponents = DeduplicateComponents(project.Components);
            var deduplicatedProject = project with { Components = deduplicatedComponents };

            if (deduplicatedComponents.Count < project.Components.Count)
            {
                var duplicateCount = project.Components.Count - deduplicatedComponents.Count;
                warnings.Add($"Removed {duplicateCount} duplicate component(s) with the same name");
                _logger.LogWarning(
                    "Removed {DuplicateCount} duplicate components from {ProjectName}",
                    duplicateCount, project.Name);
            }

            // Generate config
            var configFile = await GenerateConfigFileAsync(deduplicatedProject, fullOutputPath, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(configFile);

            // Generate fixture
            var fixtureFile = await GenerateFixtureFileAsync(deduplicatedProject, fullOutputPath, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(fixtureFile);

            // Generate helpers
            var helpersFile = await GenerateHelpersFileAsync(fullOutputPath, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(helpersFile);

            // Generate page objects, selectors, and tests for each component
            foreach (var component in deduplicatedComponents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var pageObjectFile = await GeneratePageObjectFileAsync(component, pagesDir, cancellationToken)
                        .ConfigureAwait(false);
                    generatedFiles.Add(pageObjectFile);

                    var selectorsFile = await GenerateSelectorsFileAsync(component, selectorsDir, cancellationToken)
                        .ConfigureAwait(false);
                    generatedFiles.Add(selectorsFile);

                    var testSpecFile = await GenerateTestSpecFileAsync(component, testsDir, cancellationToken)
                        .ConfigureAwait(false);
                    generatedFiles.Add(testSpecFile);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to generate files for component {component.Name}: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to generate files for component {ComponentName}", component.Name);
                }
            }

            _logger.LogInformation(
                "Generated {FileCount} files for application {ProjectName}",
                generatedFiles.Count, project.Name);

            return GenerationResult.Successful(generatedFiles, warnings.Count > 0 ? warnings : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate POM files for application {ProjectName}", project.Name);
            return GenerationResult.Failed($"Failed to generate files: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateForWorkspaceAsync(
        AngularWorkspaceInfo workspace,
        string outputPath,
        string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(outputPath);

        _logger.LogInformation("Generating POM files for workspace at {WorkspacePath}", workspace.RootPath);

        var allGeneratedFiles = new List<GeneratedFile>();
        var allWarnings = new List<string>();
        var errors = new List<string>();

        var projectsToGenerate = string.IsNullOrEmpty(projectName)
            ? workspace.Projects.Where(p => p.ProjectType == AngularProjectType.Application).ToList()
            : workspace.Projects.Where(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase)).ToList();

        if (projectsToGenerate.Count == 0)
        {
            var message = string.IsNullOrEmpty(projectName)
                ? "No application projects found in workspace"
                : $"Project '{projectName}' not found in workspace";
            return GenerationResult.Failed(message);
        }

        foreach (var project in projectsToGenerate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectOutputPath = _fileSystem.CombinePath(
                _fileSystem.GetFullPath(outputPath),
                project.Name,
                _options.OutputDirectoryName);

            var result = await GenerateForApplicationAsync(project, projectOutputPath, cancellationToken)
                .ConfigureAwait(false);

            allGeneratedFiles.AddRange(result.GeneratedFiles);

            if (result.Warnings.Count > 0)
            {
                allWarnings.AddRange(result.Warnings.Select(w => $"[{project.Name}] {w}"));
            }

            if (!result.Success)
            {
                errors.AddRange(result.Errors.Select(e => $"[{project.Name}] {e}"));
            }
        }

        if (errors.Count > 0)
        {
            return new GenerationResult
            {
                Success = false,
                GeneratedFiles = allGeneratedFiles,
                Errors = errors,
                Warnings = allWarnings
            };
        }

        return GenerationResult.Successful(allGeneratedFiles, allWarnings.Count > 0 ? allWarnings : null);
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateArtifactsAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasAnyGenerationOption)
        {
            return GenerationResult.Failed("No generation options specified");
        }

        var targetPath = _fileSystem.GetFullPath(request.TargetPath);
        var outputPath = request.OutputPath ?? _fileSystem.CombinePath(targetPath, _options.OutputDirectoryName);

        _logger.LogInformation("Generating artifacts for {TargetPath}", targetPath);

        // Determine if workspace or application
        AngularProjectInfo project;
        if (_analyzer.IsWorkspace(targetPath))
        {
            var workspace = await _analyzer.AnalyzeWorkspaceAsync(targetPath, cancellationToken)
                .ConfigureAwait(false);

            var targetProject = !string.IsNullOrEmpty(request.ProjectName)
                ? workspace.Projects.FirstOrDefault(p => p.Name.Equals(request.ProjectName, StringComparison.OrdinalIgnoreCase))
                : workspace.Projects.FirstOrDefault(p => p.ProjectType == AngularProjectType.Application);

            if (targetProject is null)
            {
                return GenerationResult.Failed(
                    !string.IsNullOrEmpty(request.ProjectName)
                        ? $"Project '{request.ProjectName}' not found"
                        : "No application project found in workspace");
            }

            project = targetProject;
            outputPath = _fileSystem.CombinePath(outputPath, project.Name);
        }
        else
        {
            project = await _analyzer.AnalyzeApplicationAsync(targetPath, cancellationToken)
                .ConfigureAwait(false);
        }

        var generatedFiles = new List<GeneratedFile>();
        var warnings = new List<string>();

        _fileSystem.CreateDirectory(outputPath);

        try
        {
            if (request.GenerateConfigs)
            {
                var configFile = await GenerateConfigFileAsync(project, outputPath, cancellationToken)
                    .ConfigureAwait(false);
                generatedFiles.Add(configFile);
            }

            if (request.GenerateFixtures)
            {
                var fixtureFile = await GenerateFixtureFileAsync(project, outputPath, cancellationToken)
                    .ConfigureAwait(false);
                generatedFiles.Add(fixtureFile);
            }

            if (request.GenerateHelpers)
            {
                var helpersFile = await GenerateHelpersFileAsync(outputPath, cancellationToken)
                    .ConfigureAwait(false);
                generatedFiles.Add(helpersFile);
            }

            if (request.GeneratePageObjects || request.GenerateSelectors)
            {
                var pagesDir = _fileSystem.CombinePath(outputPath, "pages");
                var selectorsDir = _fileSystem.CombinePath(outputPath, "selectors");

                if (request.GeneratePageObjects)
                {
                    _fileSystem.CreateDirectory(pagesDir);
                }
                if (request.GenerateSelectors)
                {
                    _fileSystem.CreateDirectory(selectorsDir);
                }

                foreach (var component in project.Components)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (request.GeneratePageObjects)
                        {
                            var pageObjectFile = await GeneratePageObjectFileAsync(component, pagesDir, cancellationToken)
                                .ConfigureAwait(false);
                            generatedFiles.Add(pageObjectFile);
                        }

                        if (request.GenerateSelectors)
                        {
                            var selectorsFile = await GenerateSelectorsFileAsync(component, selectorsDir, cancellationToken)
                                .ConfigureAwait(false);
                            generatedFiles.Add(selectorsFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to generate files for component {component.Name}: {ex.Message}");
                    }
                }
            }

            return GenerationResult.Successful(generatedFiles, warnings.Count > 0 ? warnings : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate artifacts");
            return GenerationResult.Failed($"Failed to generate artifacts: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateSignalRMockAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        _logger.LogInformation("Generating SignalR mock fixture at {OutputPath}", outputPath);

        try
        {
            var fullOutputPath = _fileSystem.GetFullPath(outputPath);
            _fileSystem.CreateDirectory(fullOutputPath);

            var content = _templateEngine.GenerateSignalRMock();
            var filePath = _fileSystem.CombinePath(fullOutputPath, "signalr-mock.fixture.ts");

            await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
                .ConfigureAwait(false);

            var generatedFile = new GeneratedFile
            {
                RelativePath = "signalr-mock.fixture.ts",
                AbsolutePath = filePath,
                FileType = GeneratedFileType.SignalRMock,
                Content = content
            };

            _logger.LogInformation("Generated SignalR mock fixture at {FilePath}", filePath);

            return GenerationResult.Successful([generatedFile]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SignalR mock fixture");
            return GenerationResult.Failed($"Failed to generate SignalR mock: {ex.Message}");
        }
    }

    private async Task<GeneratedFile> GenerateConfigFileAsync(
        AngularProjectInfo project,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateConfig(project);
        var filePath = _fileSystem.CombinePath(outputPath, "playwright.config.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "playwright.config.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Config,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateFixtureFileAsync(
        AngularProjectInfo project,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateFixture(project);
        var filePath = _fileSystem.CombinePath(outputPath, "fixtures.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "fixtures.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Fixture,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateHelpersFileAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateHelpers();
        var filePath = _fileSystem.CombinePath(outputPath, "helpers.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "helpers.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Helper,
            Content = content
        };
    }

    private async Task<GeneratedFile> GeneratePageObjectFileAsync(
        AngularComponentInfo component,
        string pagesDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GeneratePageObject(component);
        var fileName = $"{ToKebabCase(component.Name)}.page.ts";
        var filePath = _fileSystem.CombinePath(pagesDir, fileName);

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = $"pages/{fileName}",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.PageObject,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateSelectorsFileAsync(
        AngularComponentInfo component,
        string selectorsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateSelectors(component);
        var fileName = $"{ToKebabCase(component.Name)}.selectors.ts";
        var filePath = _fileSystem.CombinePath(selectorsDir, fileName);

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = $"selectors/{fileName}",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Selectors,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateTestSpecFileAsync(
        AngularComponentInfo component,
        string testsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateTestSpec(component);
        var fileName = $"{ToKebabCase(component.Name)}.{_options.TestFileSuffix}.ts";
        var filePath = _fileSystem.CombinePath(testsDir, fileName);

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = $"tests/{fileName}",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.TestSpec,
            Content = content
        };
    }

    private static List<AngularComponentInfo> DeduplicateComponents(IReadOnlyList<AngularComponentInfo> components)
    {
        var result = new Dictionary<string, AngularComponentInfo>(StringComparer.Ordinal);

        foreach (var component in components)
        {
            if (!result.TryGetValue(component.Name, out var existing))
            {
                // First occurrence, add it
                result[component.Name] = component;
            }
            else
            {
                // Duplicate found - merge selectors from this component into the existing one
                if (component.Selectors.Count > existing.Selectors.Count)
                {
                    // If the new component has more selectors, use it instead
                    result[component.Name] = component;
                }
                else if (component.Selectors.Count > 0 && existing.Selectors.Count == 0)
                {
                    // If existing has no selectors but this one does, use this one
                    result[component.Name] = component;
                }
                // Otherwise keep the existing one
            }
        }

        return [.. result.Values];
    }

    private static string ToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    result.Append('-');
                }
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        var str = result.ToString();
        if (str.EndsWith("-component", StringComparison.Ordinal))
        {
            str = str[..^"-component".Length];
        }

        return str;
    }
}
