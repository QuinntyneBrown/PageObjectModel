using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Analyzes Angular applications and workspaces to extract component and routing information.
/// </summary>
public sealed partial class AngularAnalyzer : IAngularAnalyzer
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<AngularAnalyzer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AngularAnalyzer"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system service.</param>
    /// <param name="logger">The logger.</param>
    public AngularAnalyzer(IFileSystem fileSystem, ILogger<AngularAnalyzer> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsWorkspace(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var angularJsonPath = _fileSystem.CombinePath(path, "angular.json");
        return _fileSystem.FileExists(angularJsonPath);
    }

    /// <inheritdoc />
    public bool IsApplication(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Check for angular.json (workspace) or package.json with @angular/core
        if (IsWorkspace(path))
        {
            return true;
        }

        var packageJsonPath = _fileSystem.CombinePath(path, "package.json");
        if (!_fileSystem.FileExists(packageJsonPath))
        {
            return false;
        }

        try
        {
            var content = _fileSystem.ReadAllTextAsync(packageJsonPath).GetAwaiter().GetResult();
            return content.Contains("@angular/core");
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AngularWorkspaceInfo> AnalyzeWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspacePath);

        var fullPath = _fileSystem.GetFullPath(workspacePath);
        _logger.LogInformation("Analyzing Angular workspace at {Path}", fullPath);

        var angularJsonPath = _fileSystem.CombinePath(fullPath, "angular.json");
        if (!_fileSystem.FileExists(angularJsonPath))
        {
            throw new InvalidOperationException($"No angular.json found at {fullPath}");
        }

        var angularJsonContent = await _fileSystem.ReadAllTextAsync(angularJsonPath, cancellationToken)
            .ConfigureAwait(false);

        using var document = JsonDocument.Parse(angularJsonContent, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip
        });

        var root = document.RootElement;
        var cliVersion = root.TryGetProperty("version", out var versionProp)
            ? versionProp.GetInt32().ToString()
            : null;

        string? defaultProject = null;
        if (root.TryGetProperty("defaultProject", out var defaultProjectProp))
        {
            defaultProject = defaultProjectProp.GetString();
        }

        var projects = new List<AngularProjectInfo>();

        if (root.TryGetProperty("projects", out var projectsElement))
        {
            foreach (var projectProp in projectsElement.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var projectName = projectProp.Name;
                var projectElement = projectProp.Value;

                var projectType = GetProjectType(projectElement);
                if (projectType == AngularProjectType.Library)
                {
                    _logger.LogDebug("Skipping library project {ProjectName}", projectName);
                    continue;
                }

                var projectRoot = projectElement.TryGetProperty("root", out var rootProp)
                    ? rootProp.GetString() ?? ""
                    : "";

                var sourceRoot = projectElement.TryGetProperty("sourceRoot", out var sourceRootProp)
                    ? sourceRootProp.GetString() ?? _fileSystem.CombinePath(projectRoot, "src")
                    : _fileSystem.CombinePath(projectRoot, "src");

                var projectPath = _fileSystem.CombinePath(fullPath, projectRoot);
                var sourcePath = _fileSystem.CombinePath(fullPath, sourceRoot);

                var components = await AnalyzeComponentsAsync(sourcePath, cancellationToken)
                    .ConfigureAwait(false);

                var routes = await AnalyzeRoutesAsync(sourcePath, cancellationToken)
                    .ConfigureAwait(false);

                projects.Add(new AngularProjectInfo
                {
                    Name = projectName,
                    RootPath = projectPath,
                    SourceRoot = sourcePath,
                    ProjectType = projectType,
                    Components = components,
                    Routes = routes
                });

                _logger.LogInformation(
                    "Analyzed project {ProjectName}: {ComponentCount} components, {RouteCount} routes",
                    projectName, components.Count, routes.Count);
            }
        }

        return new AngularWorkspaceInfo
        {
            RootPath = fullPath,
            CliVersion = cliVersion,
            DefaultProject = defaultProject,
            Projects = projects
        };
    }

    /// <inheritdoc />
    public async Task<AngularProjectInfo> AnalyzeApplicationAsync(
        string applicationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationPath);

        var fullPath = _fileSystem.GetFullPath(applicationPath);
        _logger.LogInformation("Analyzing Angular application at {Path}", fullPath);

        // If this is a workspace, analyze and return the default project
        if (IsWorkspace(fullPath))
        {
            var workspace = await AnalyzeWorkspaceAsync(fullPath, cancellationToken).ConfigureAwait(false);

            var project = !string.IsNullOrEmpty(workspace.DefaultProject)
                ? workspace.Projects.FirstOrDefault(p => p.Name == workspace.DefaultProject)
                : workspace.Projects.FirstOrDefault(p => p.ProjectType == AngularProjectType.Application);

            if (project is null)
            {
                throw new InvalidOperationException("No application project found in workspace");
            }

            return project;
        }

        // Standalone application structure
        var srcPath = _fileSystem.CombinePath(fullPath, "src");
        if (!_fileSystem.DirectoryExists(srcPath))
        {
            srcPath = fullPath;
        }

        var components = await AnalyzeComponentsAsync(srcPath, cancellationToken).ConfigureAwait(false);
        var routes = await AnalyzeRoutesAsync(srcPath, cancellationToken).ConfigureAwait(false);

        var projectName = _fileSystem.GetFileName(fullPath) ?? "app";

        return new AngularProjectInfo
        {
            Name = projectName,
            RootPath = fullPath,
            SourceRoot = srcPath,
            ProjectType = AngularProjectType.Application,
            Components = components,
            Routes = routes
        };
    }

    private static AngularProjectType GetProjectType(JsonElement projectElement)
    {
        if (projectElement.TryGetProperty("projectType", out var typeProp))
        {
            var typeString = typeProp.GetString();
            return typeString?.Equals("library", StringComparison.OrdinalIgnoreCase) == true
                ? AngularProjectType.Library
                : AngularProjectType.Application;
        }

        return AngularProjectType.Application;
    }

    private async Task<List<AngularComponentInfo>> AnalyzeComponentsAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var components = new List<AngularComponentInfo>();

        if (!_fileSystem.DirectoryExists(sourcePath))
        {
            return components;
        }

        // Get both traditional *.component.ts files and modern *.ts files
        // Modern Angular standalone components may not use .component.ts suffix
        var componentFiles = _fileSystem.GetFiles(sourcePath, "*.component.ts", recursive: true)
            .Concat(_fileSystem.GetFiles(sourcePath, "*.ts", recursive: true))
            .Where(f => !f.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".module.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".service.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".guard.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".interceptor.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".model.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".pipe.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".directive.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".config.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.EndsWith(".routes.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.Contains("index.ts", StringComparison.OrdinalIgnoreCase) &&
                       !f.Contains("main.ts", StringComparison.OrdinalIgnoreCase))
            .Distinct();

        foreach (var filePath in componentFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken)
                    .ConfigureAwait(false);

                var component = ParseComponent(filePath, content);
                if (component is not null)
                {
                    // Try to find and parse the template
                    var templatePath = GetTemplatePath(filePath, content);
                    if (templatePath is not null && _fileSystem.FileExists(templatePath))
                    {
                        var templateContent = await _fileSystem.ReadAllTextAsync(templatePath, cancellationToken)
                            .ConfigureAwait(false);
                        var selectors = ParseTemplateSelectors(templateContent);

                        component = component with
                        {
                            TemplatePath = templatePath,
                            Selectors = selectors
                        };
                    }

                    components.Add(component);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze component at {Path}", filePath);
            }
        }

        return components;
    }

    private AngularComponentInfo? ParseComponent(string filePath, string content)
    {
        // Extract component decorator
        var decoratorMatch = ComponentDecoratorRegex().Match(content);
        if (!decoratorMatch.Success)
        {
            return null;
        }

        // Extract selector
        var selectorMatch = SelectorRegex().Match(decoratorMatch.Value);
        var selector = selectorMatch.Success ? selectorMatch.Groups[1].Value : "unknown";

        // Extract component class name
        var classMatch = ComponentClassRegex().Match(content);
        var className = classMatch.Success ? classMatch.Groups[1].Value : "UnknownComponent";

        // Extract inputs
        var inputs = InputRegex().Matches(content)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();

        // Extract outputs
        var outputs = OutputRegex().Matches(content)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();

        return new AngularComponentInfo
        {
            Name = className,
            Selector = selector,
            FilePath = filePath,
            Inputs = inputs,
            Outputs = outputs
        };
    }

    private string? GetTemplatePath(string componentPath, string content)
    {
        // Check for templateUrl
        var templateUrlMatch = TemplateUrlRegex().Match(content);
        if (templateUrlMatch.Success)
        {
            var templateUrl = templateUrlMatch.Groups[1].Value;
            var directory = _fileSystem.GetDirectoryName(componentPath);
            if (directory is not null)
            {
                return _fileSystem.CombinePath(directory, templateUrl);
            }
        }

        // Check for inline template
        if (content.Contains("template:"))
        {
            return null;
        }

        // Convention-based: try multiple naming conventions
        // Traditional: *.component.html
        var htmlPath = componentPath.Replace(".component.ts", ".component.html");
        if (_fileSystem.FileExists(htmlPath))
        {
            return htmlPath;
        }

        // Modern: *.html (same base name as .ts file)
        htmlPath = componentPath.Replace(".ts", ".html");
        if (_fileSystem.FileExists(htmlPath))
        {
            return htmlPath;
        }

        return null;
    }

    private List<ElementSelector> ParseTemplateSelectors(string templateContent)
    {
        var selectors = new List<ElementSelector>();

        // Parse data-testid attributes (highest priority)
        foreach (Match match in DataTestIdRegex().Matches(templateContent))
        {
            var testId = match.Groups[1].Value;
            var elementType = ExtractElementType(templateContent, match.Index);
            selectors.Add(new ElementSelector
            {
                ElementType = elementType,
                Strategy = SelectorStrategy.TestId,
                SelectorValue = $"[data-testid='{testId}']",
                PropertyName = ToPascalCase(testId)
            });
        }

        // Parse id attributes
        foreach (Match match in IdAttributeRegex().Matches(templateContent))
        {
            var id = match.Groups[1].Value;
            if (selectors.Any(s => s.PropertyName == ToPascalCase(id)))
            {
                continue;
            }

            var elementType = ExtractElementType(templateContent, match.Index);
            selectors.Add(new ElementSelector
            {
                ElementType = elementType,
                Strategy = SelectorStrategy.Id,
                SelectorValue = $"#{id}",
                PropertyName = ToPascalCase(id)
            });
        }

        // Parse buttons with text
        foreach (Match match in ButtonWithTextRegex().Matches(templateContent))
        {
            var buttonText = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(buttonText) || buttonText.Contains("{{"))
            {
                continue;
            }

            var propertyName = ToPascalCase(buttonText) + "Button";
            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                continue;
            }

            selectors.Add(new ElementSelector
            {
                ElementType = "button",
                Strategy = SelectorStrategy.Role,
                SelectorValue = $"button:has-text(\"{buttonText}\")",
                PropertyName = propertyName,
                TextContent = buttonText
            });
        }

        // Parse inputs with formControlName
        foreach (Match match in FormControlNameRegex().Matches(templateContent))
        {
            var controlName = match.Groups[1].Value;
            var propertyName = ToPascalCase(controlName) + "Input";
            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                continue;
            }

            selectors.Add(new ElementSelector
            {
                ElementType = "input",
                Strategy = SelectorStrategy.Css,
                SelectorValue = $"[formControlName='{controlName}']",
                PropertyName = propertyName
            });
        }

        return selectors;
    }

    private static string ExtractElementType(string content, int position)
    {
        // Walk backwards to find the opening tag
        var tagStart = content.LastIndexOf('<', position);
        if (tagStart < 0)
        {
            return "unknown";
        }

        var tagContent = content[tagStart..position];
        var tagMatch = ElementTagRegex().Match(tagContent);
        return tagMatch.Success ? tagMatch.Groups[1].Value.ToLowerInvariant() : "unknown";
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Remove special characters and split by common delimiters
        var words = NonAlphanumericRegex().Split(input)
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());

        return string.Concat(words);
    }

    private async Task<List<RouteInfo>> AnalyzeRoutesAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var routes = new List<RouteInfo>();

        if (!_fileSystem.DirectoryExists(sourcePath))
        {
            return routes;
        }

        // Look for routing modules
        var routingFiles = _fileSystem.GetFiles(sourcePath, "*routing*.ts", recursive: true)
            .Concat(_fileSystem.GetFiles(sourcePath, "*routes*.ts", recursive: true))
            .Distinct();

        foreach (var filePath in routingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken)
                    .ConfigureAwait(false);

                var fileRoutes = ParseRoutes(content);
                routes.AddRange(fileRoutes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse routes from {Path}", filePath);
            }
        }

        return routes;
    }

    private static List<RouteInfo> ParseRoutes(string content)
    {
        var routes = new List<RouteInfo>();

        // Simple route parsing - look for path definitions
        foreach (Match match in RoutePathRegex().Matches(content))
        {
            var path = match.Groups[1].Value;

            // Try to find associated component
            var componentMatch = RouteComponentRegex().Match(content[match.Index..]);
            var component = componentMatch.Success ? componentMatch.Groups[1].Value : null;

            // Check for redirect
            var redirectMatch = RouteRedirectRegex().Match(content[match.Index..]);
            var redirectTo = redirectMatch.Success ? redirectMatch.Groups[1].Value : null;

            // Check for lazy loading
            var isLazy = content[(match.Index)..].Contains("loadChildren") ||
                        content[(match.Index)..].Contains("loadComponent");

            routes.Add(new RouteInfo
            {
                Path = path,
                Component = component,
                RedirectTo = redirectTo,
                IsLazyLoaded = isLazy
            });
        }

        return routes;
    }

    [GeneratedRegex(@"@Component\s*\(\s*\{[^}]+\}", RegexOptions.Singleline)]
    private static partial Regex ComponentDecoratorRegex();

    [GeneratedRegex(@"selector\s*:\s*['""]([^'""]+)['""]")]
    private static partial Regex SelectorRegex();

    [GeneratedRegex(@"export\s+class\s+(\w+)")]
    private static partial Regex ComponentClassRegex();

    [GeneratedRegex(@"@Input\(\)\s*(\w+)")]
    private static partial Regex InputRegex();

    [GeneratedRegex(@"@Output\(\)\s*(\w+)")]
    private static partial Regex OutputRegex();

    [GeneratedRegex(@"templateUrl\s*:\s*['""]([^'""]+)['""]")]
    private static partial Regex TemplateUrlRegex();

    [GeneratedRegex(@"data-testid\s*=\s*['""]([^'""]+)['""]")]
    private static partial Regex DataTestIdRegex();

    [GeneratedRegex(@"\bid\s*=\s*['""]([^'""]+)['""]")]
    private static partial Regex IdAttributeRegex();

    [GeneratedRegex(@"<button[^>]*>([^<]+)</button>", RegexOptions.IgnoreCase)]
    private static partial Regex ButtonWithTextRegex();

    [GeneratedRegex(@"formControlName\s*=\s*['""]([^'""]+)['""]")]
    private static partial Regex FormControlNameRegex();

    [GeneratedRegex(@"<(\w+)")]
    private static partial Regex ElementTagRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"path\s*:\s*['""]([^'""]*)['""]")]
    private static partial Regex RoutePathRegex();

    [GeneratedRegex(@"component\s*:\s*(\w+)")]
    private static partial Regex RouteComponentRegex();

    [GeneratedRegex(@"redirectTo\s*:\s*['""]([^'""]+)['""]")]
    private static partial Regex RouteRedirectRegex();
}
