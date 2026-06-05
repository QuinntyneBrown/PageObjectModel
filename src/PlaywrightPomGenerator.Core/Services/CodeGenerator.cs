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
            var pageObjectsDir = _fileSystem.CombinePath(fullOutputPath, "page-objects");
            var helpersDir = _fileSystem.CombinePath(fullOutputPath, "helpers");
            var testsDir = _fileSystem.CombinePath(fullOutputPath, "tests");
            var configsDir = _fileSystem.CombinePath(fullOutputPath, "configs");
            var fixturesDir = _fileSystem.CombinePath(fullOutputPath, "fixtures");

            _fileSystem.CreateDirectory(pageObjectsDir);
            _fileSystem.CreateDirectory(helpersDir);
            _fileSystem.CreateDirectory(testsDir);
            _fileSystem.CreateDirectory(configsDir);
            _fileSystem.CreateDirectory(fixturesDir);

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

            // Generate playwright config
            var configFile = await GenerateConfigFileAsync(deduplicatedProject, fullOutputPath, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(configFile);

            // Generate timeout config
            var timeoutConfigFile = await GenerateTimeoutConfigFileAsync(configsDir, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(timeoutConfigFile);

            // Generate urls config
            var urlsConfigFile = await GenerateUrlsConfigFileAsync(deduplicatedProject, configsDir, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(urlsConfigFile);

            // Generate fixture
            var fixtureFile = await GenerateFixtureFileAsync(deduplicatedProject, fixturesDir, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(fixtureFile);

            // Generate helpers utility file
            var helpersUtilFile = await GenerateHelpersUtilFileAsync(fullOutputPath, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(helpersUtilFile);

            // Generate base page class
            var basePageFile = await GenerateBasePageFileAsync(pageObjectsDir, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(basePageFile);

            // Generate page objects, selectors, and tests for each component
            foreach (var component in deduplicatedComponents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var pageObjectFile = await GeneratePageObjectFileAsync(component, pageObjectsDir, cancellationToken)
                        .ConfigureAwait(false);
                    generatedFiles.Add(pageObjectFile);

                    var selectorsFile = await GenerateSelectorsFileAsync(component, helpersDir, cancellationToken)
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
                var helpersFile = await GenerateHelpersUtilFileAsync(outputPath, cancellationToken)
                    .ConfigureAwait(false);
                generatedFiles.Add(helpersFile);
            }

            if (request.GeneratePageObjects || request.GenerateSelectors)
            {
                var pageObjectsDir = _fileSystem.CombinePath(outputPath, "page-objects");
                var helpersDir = _fileSystem.CombinePath(outputPath, "helpers");

                if (request.GeneratePageObjects)
                {
                    _fileSystem.CreateDirectory(pageObjectsDir);

                    // Generate base page class first
                    var basePageFile = await GenerateBasePageFileAsync(pageObjectsDir, cancellationToken)
                        .ConfigureAwait(false);
                    generatedFiles.Add(basePageFile);
                }
                if (request.GenerateSelectors)
                {
                    _fileSystem.CreateDirectory(helpersDir);
                }

                foreach (var component in project.Components)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (request.GeneratePageObjects)
                        {
                            var pageObjectFile = await GeneratePageObjectFileAsync(component, pageObjectsDir, cancellationToken)
                                .ConfigureAwait(false);
                            generatedFiles.Add(pageObjectFile);
                        }

                        if (request.GenerateSelectors)
                        {
                            var selectorsFile = await GenerateSelectorsFileAsync(component, helpersDir, cancellationToken)
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

            if (request.GenerateComponentObjects)
            {
                var componentObjectsDir = _fileSystem.CombinePath(outputPath, "components");
                _fileSystem.CreateDirectory(componentObjectsDir);

                // Generate the base component class once.
                var baseComponentFile = await GenerateBaseComponentFileAsync(componentObjectsDir, cancellationToken)
                    .ConfigureAwait(false);
                generatedFiles.Add(baseComponentFile);

                foreach (var component in project.Components)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var componentObjectFile = await GenerateComponentObjectFileAsync(component, componentObjectsDir, cancellationToken)
                            .ConfigureAwait(false);
                        generatedFiles.Add(componentObjectFile);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to generate component object for {component.Name}: {ex.Message}");
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

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateComponentObjectsAsync(
        AngularProjectInfo project,
        string outputPath,
        bool excludeRoutable = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(outputPath);

        _logger.LogInformation("Generating component objects for {ProjectName}", project.Name);

        var generatedFiles = new List<GeneratedFile>();
        var warnings = new List<string>();

        try
        {
            var fullOutputPath = _fileSystem.GetFullPath(outputPath);
            _fileSystem.CreateDirectory(fullOutputPath);

            var componentObjectsDir = _fileSystem.CombinePath(fullOutputPath, "components");
            var testsDir = _fileSystem.CombinePath(fullOutputPath, "tests");
            _fileSystem.CreateDirectory(componentObjectsDir);
            _fileSystem.CreateDirectory(testsDir);

            // Deduplicate components by name (keep first occurrence, merge selectors from duplicates)
            var deduplicatedComponents = DeduplicateComponents(project.Components);

            if (deduplicatedComponents.Count < project.Components.Count)
            {
                var duplicateCount = project.Components.Count - deduplicatedComponents.Count;
                warnings.Add($"Removed {duplicateCount} duplicate component(s) with the same name");
            }

            var targets = excludeRoutable
                ? deduplicatedComponents.Where(c => !c.IsRoutable).ToList()
                : deduplicatedComponents;

            if (excludeRoutable && targets.Count < deduplicatedComponents.Count)
            {
                var skipped = deduplicatedComponents.Count - targets.Count;
                warnings.Add($"Skipped {skipped} routable component(s) because --exclude-routable was set");
            }

            // Generate the base component class once.
            var baseComponentFile = await GenerateBaseComponentFileAsync(componentObjectsDir, cancellationToken)
                .ConfigureAwait(false);
            generatedFiles.Add(baseComponentFile);

            foreach (var component in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (component.Selectors.Count == 0)
                    {
                        warnings.Add(
                            $"Component {component.Name} has no detected selectors; generated a minimal " +
                            "component object (hostSelector + expectVisible/expectHidden only)");
                    }

                    var componentObjectFile = await GenerateComponentObjectFileAsync(component, componentObjectsDir, cancellationToken)
                        .ConfigureAwait(false);
                    generatedFiles.Add(componentObjectFile);

                    var specFile = await GenerateComponentObjectTestSpecFileAsync(component, testsDir, cancellationToken)
                        .ConfigureAwait(false);
                    generatedFiles.Add(specFile);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to generate component object for {component.Name}: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to generate component object for {ComponentName}", component.Name);
                }
            }

            _logger.LogInformation(
                "Generated {FileCount} component-object files for {ProjectName}",
                generatedFiles.Count, project.Name);

            return GenerationResult.Successful(generatedFiles, warnings.Count > 0 ? warnings : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate component objects for {ProjectName}", project.Name);
            return GenerationResult.Failed($"Failed to generate component objects: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateBridgeAsync(
        IReadOnlyList<InjectionTokenInterface> interfaces,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(outputPath);

        _logger.LogInformation("Generating Playwright bridge for {Count} interface(s)", interfaces.Count);

        var generatedFiles = new List<GeneratedFile>();
        var warnings = new List<string>();

        try
        {
            var fullOutputPath = _fileSystem.GetFullPath(outputPath);
            _fileSystem.CreateDirectory(fullOutputPath);

            var bridgeDir = _fileSystem.CombinePath(fullOutputPath, "bridge");
            var mocksDir = _fileSystem.CombinePath(bridgeDir, "mocks");
            _fileSystem.CreateDirectory(bridgeDir);
            _fileSystem.CreateDirectory(mocksDir);

            var resolved = new List<InjectionTokenInterface>();
            foreach (var token in interfaces)
            {
                if (token.InterfaceFilePath is null)
                {
                    warnings.Add($"Interface {token.InterfaceName} (token {token.TokenName}) could not be resolved; skipped.");
                    continue;
                }

                if (token.Members.Count == 0)
                {
                    warnings.Add($"Interface {token.InterfaceName} has no members; generated an empty mock.");
                }

                resolved.Add(EnrichBridgeInterface(token, bridgeDir, mocksDir));
            }

            if (resolved.Count == 0)
            {
                return new GenerationResult
                {
                    Success = false,
                    Errors = ["No injection-token-backed interfaces were resolved."],
                    Warnings = warnings
                };
            }

            var registryFile = await WriteBridgeFileAsync(
                _fileSystem.CombinePath(bridgeDir, "bridge-registry.ts"),
                "bridge/bridge-registry.ts",
                _templateEngine.GenerateBridgeRegistry(),
                cancellationToken).ConfigureAwait(false);
            generatedFiles.Add(registryFile);

            var providersFile = await WriteBridgeFileAsync(
                _fileSystem.CombinePath(bridgeDir, "bridge-providers.ts"),
                "bridge/bridge-providers.ts",
                _templateEngine.GenerateBridgeProviders(resolved),
                cancellationToken).ConfigureAwait(false);
            generatedFiles.Add(providersFile);

            var clientFile = await WriteBridgeFileAsync(
                _fileSystem.CombinePath(bridgeDir, "playwright-bridge.ts"),
                "bridge/playwright-bridge.ts",
                _templateEngine.GeneratePlaywrightBridge(resolved),
                cancellationToken).ConfigureAwait(false);
            generatedFiles.Add(clientFile);

            foreach (var token in resolved)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileName = $"{token.MockFileStem}.ts";
                    var mockFile = await WriteBridgeFileAsync(
                        _fileSystem.CombinePath(mocksDir, fileName),
                        $"bridge/mocks/{fileName}",
                        _templateEngine.GenerateInterfaceMock(token),
                        cancellationToken).ConfigureAwait(false);
                    generatedFiles.Add(mockFile);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to generate mock for {token.InterfaceName}: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to generate mock for {InterfaceName}", token.InterfaceName);
                }
            }

            return GenerationResult.Successful(generatedFiles, warnings.Count > 0 ? warnings : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Playwright bridge");
            return GenerationResult.Failed($"Failed to generate bridge: {ex.Message}");
        }
    }

    private async Task<GeneratedFile> WriteBridgeFileAsync(
        string filePath,
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = relativePath,
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Bridge,
            Content = content
        };
    }

    private static InjectionTokenInterface EnrichBridgeInterface(InjectionTokenInterface token, string bridgeDir, string mocksDir)
    {
        var stripped = StripInterfacePrefix(token.InterfaceName);
        return token with
        {
            MockClassName = stripped + "Mock",
            MockFileStem = ToKebabCase(stripped) + ".mock",
            PlaywrightAccessor = ToCamelCase(stripped),
            TokenImportPath = ToRelativeImport(bridgeDir, token.TokenFilePath),
            InterfaceImportPath = token.InterfaceFilePath is null ? null : ToRelativeImport(mocksDir, token.InterfaceFilePath)
        };
    }

    private static string ToRelativeImport(string fromDir, string toFile)
    {
        var relative = Path.GetRelativePath(fromDir, toFile).Replace('\\', '/');
        if (relative.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[..^3];
        }
        if (!relative.StartsWith("./", StringComparison.Ordinal) && !relative.StartsWith("../", StringComparison.Ordinal))
        {
            relative = "./" + relative;
        }
        return relative;
    }

    private static string StripInterfacePrefix(string name) =>
        name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]) ? name[1..] : name;

    private static string ToCamelCase(string input) =>
        string.IsNullOrEmpty(input) ? input : char.ToLowerInvariant(input[0]) + input[1..];

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
        string fixturesDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateFixture(project);
        var filePath = _fileSystem.CombinePath(fixturesDir, "fixtures.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "fixtures/fixtures.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Fixture,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateHelpersUtilFileAsync(
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

    private async Task<GeneratedFile> GenerateTimeoutConfigFileAsync(
        string configsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateTimeoutConfig();
        var filePath = _fileSystem.CombinePath(configsDir, "timeout.config.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "configs/timeout.config.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Config,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateUrlsConfigFileAsync(
        AngularProjectInfo project,
        string configsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateUrlsConfig(project);
        var filePath = _fileSystem.CombinePath(configsDir, "urls.config.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "configs/urls.config.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.Config,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateBasePageFileAsync(
        string pageObjectsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateBasePage();
        var filePath = _fileSystem.CombinePath(pageObjectsDir, "base.page.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "page-objects/base.page.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.PageObject,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateBaseComponentFileAsync(
        string componentObjectsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateBaseComponent();
        var filePath = _fileSystem.CombinePath(componentObjectsDir, "base.component.ts");

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = "components/base.component.ts",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.ComponentObject,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateComponentObjectFileAsync(
        AngularComponentInfo component,
        string componentObjectsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateComponentObject(component);
        var fileName = $"{ToKebabCase(component.Name)}.component.ts";
        var filePath = _fileSystem.CombinePath(componentObjectsDir, fileName);

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = $"components/{fileName}",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.ComponentObject,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateComponentObjectTestSpecFileAsync(
        AngularComponentInfo component,
        string testsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateComponentObjectTestSpec(component);
        var fileName = $"{ToKebabCase(component.Name)}.component.{_options.TestFileSuffix}.ts";
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

    private async Task<GeneratedFile> GeneratePageObjectFileAsync(
        AngularComponentInfo component,
        string pageObjectsDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GeneratePageObject(component);
        var fileName = $"{ToKebabCase(component.Name)}.page.ts";
        var filePath = _fileSystem.CombinePath(pageObjectsDir, fileName);

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = $"page-objects/{fileName}",
            AbsolutePath = filePath,
            FileType = GeneratedFileType.PageObject,
            Content = content
        };
    }

    private async Task<GeneratedFile> GenerateSelectorsFileAsync(
        AngularComponentInfo component,
        string helpersDir,
        CancellationToken cancellationToken)
    {
        var content = _templateEngine.GenerateSelectors(component);
        var fileName = $"{ToKebabCase(component.Name)}.selectors.ts";
        var filePath = _fileSystem.CombinePath(helpersDir, fileName);

        await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedFile
        {
            RelativePath = $"helpers/{fileName}",
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
