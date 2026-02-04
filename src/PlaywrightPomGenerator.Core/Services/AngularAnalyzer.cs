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
    public bool IsLibrary(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Angular libraries have ng-package.json in their root
        var ngPackagePath = _fileSystem.CombinePath(path, "ng-package.json");
        return _fileSystem.FileExists(ngPackagePath);
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

        // Check if this is actually a library
        var projectType = IsLibrary(fullPath)
            ? AngularProjectType.Library
            : AngularProjectType.Application;

        return new AngularProjectInfo
        {
            Name = projectName,
            RootPath = fullPath,
            SourceRoot = srcPath,
            ProjectType = projectType,
            Components = components,
            Routes = routes
        };
    }

    /// <inheritdoc />
    public async Task<AngularProjectInfo> AnalyzeLibraryAsync(
        string libraryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(libraryPath);

        var fullPath = _fileSystem.GetFullPath(libraryPath);
        _logger.LogInformation("Analyzing Angular library at {Path}", fullPath);

        // If this is a workspace, try to find a library project
        if (IsWorkspace(fullPath))
        {
            var workspace = await AnalyzeWorkspaceAsync(fullPath, cancellationToken).ConfigureAwait(false);

            var library = workspace.Projects.FirstOrDefault(p => p.ProjectType == AngularProjectType.Library);

            if (library is null)
            {
                throw new InvalidOperationException("No library project found in workspace");
            }

            return library;
        }

        // Standalone library structure
        var srcPath = _fileSystem.CombinePath(fullPath, "src");
        if (!_fileSystem.DirectoryExists(srcPath))
        {
            // Libraries often have src/lib structure
            var libPath = _fileSystem.CombinePath(fullPath, "src", "lib");
            if (_fileSystem.DirectoryExists(libPath))
            {
                srcPath = libPath;
            }
            else
            {
                srcPath = fullPath;
            }
        }

        var components = await AnalyzeComponentsAsync(srcPath, cancellationToken).ConfigureAwait(false);
        var routes = await AnalyzeRoutesAsync(srcPath, cancellationToken).ConfigureAwait(false);

        var projectName = _fileSystem.GetFileName(fullPath) ?? "lib";

        return new AngularProjectInfo
        {
            Name = projectName,
            RootPath = fullPath,
            SourceRoot = srcPath,
            ProjectType = AngularProjectType.Library,
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
                    // Try to find and parse the template (external file or inline)
                    var templatePath = GetTemplatePath(filePath, content);
                    string? templateContent = null;

                    if (templatePath is not null && _fileSystem.FileExists(templatePath))
                    {
                        // External template file
                        templateContent = await _fileSystem.ReadAllTextAsync(templatePath, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        // Try to extract inline template
                        templateContent = ExtractInlineTemplate(content);
                    }

                    if (!string.IsNullOrEmpty(templateContent))
                    {
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

        // Check for inline template - return null to trigger inline template extraction
        if (content.Contains("template:") || content.Contains("template`"))
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

    private static string? ExtractInlineTemplate(string componentContent)
    {
        // Try to extract template content using backticks (template literals)
        var backtickMatch = InlineTemplateBacktickRegex().Match(componentContent);
        if (backtickMatch.Success)
        {
            return backtickMatch.Groups[1].Value;
        }

        // Try to extract template content using single quotes
        var singleQuoteMatch = InlineTemplateSingleQuoteRegex().Match(componentContent);
        if (singleQuoteMatch.Success)
        {
            return singleQuoteMatch.Groups[1].Value;
        }

        // Try to extract template content using double quotes
        var doubleQuoteMatch = InlineTemplateDoubleQuoteRegex().Match(componentContent);
        if (doubleQuoteMatch.Success)
        {
            return doubleQuoteMatch.Groups[1].Value;
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

        // Parse input elements with type attribute (checkbox, text, email, etc.)
        foreach (Match match in InputWithTypeRegex().Matches(templateContent))
        {
            var inputType = match.Groups[1].Value.ToLowerInvariant();
            var fullMatch = match.Value;

            // Skip if already handled by formControlName
            if (fullMatch.Contains("formControlName"))
            {
                continue;
            }

            // Try to get a meaningful name from placeholder or class
            var placeholder = ExtractAttribute(fullMatch, "placeholder");
            var className = ExtractAttribute(fullMatch, "class");

            string propertyName;
            string? textContent = null;

            if (!string.IsNullOrEmpty(placeholder) && !placeholder.Contains("{{"))
            {
                propertyName = ToPascalCase(placeholder) + "Input";
                textContent = placeholder;
            }
            else if (inputType == "checkbox")
            {
                propertyName = "Checkbox";
            }
            else if (!string.IsNullOrEmpty(className))
            {
                // Try to derive from class name
                var classWords = className.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
                var meaningfulWord = classWords.FirstOrDefault(w =>
                    !w.StartsWith("cb-", StringComparison.OrdinalIgnoreCase) &&
                    !w.Equals("form", StringComparison.OrdinalIgnoreCase) &&
                    !w.Equals("input", StringComparison.OrdinalIgnoreCase));
                propertyName = meaningfulWord is not null
                    ? ToPascalCase(meaningfulWord) + "Input"
                    : ToPascalCase(inputType) + "Input";
            }
            else
            {
                propertyName = ToPascalCase(inputType) + "Input";
            }

            // Make name unique if duplicate
            var baseName = propertyName;
            var counter = 1;
            while (selectors.Any(s => s.PropertyName == propertyName))
            {
                propertyName = $"{baseName}{counter++}";
            }

            var selectorValue = inputType == "checkbox"
                ? "input[type='checkbox']"
                : !string.IsNullOrEmpty(placeholder)
                    ? $"input[placeholder='{placeholder}']"
                    : $"input[type='{inputType}']";

            selectors.Add(new ElementSelector
            {
                ElementType = "input",
                Strategy = !string.IsNullOrEmpty(placeholder) ? SelectorStrategy.Placeholder : SelectorStrategy.Css,
                SelectorValue = selectorValue,
                PropertyName = propertyName,
                TextContent = textContent
            });
        }

        // Parse elements with click handlers
        foreach (Match match in ClickHandlerRegex().Matches(templateContent))
        {
            var handlerName = match.Groups[1].Value;
            var elementType = ExtractElementType(templateContent, match.Index);
            var textContent = ExtractTextContent(templateContent, match.Index);

            // Derive property name - prefer text content, fall back to handler name
            string propertyName;
            string selectorValue;
            SelectorStrategy strategy;

            if (!string.IsNullOrWhiteSpace(textContent) && !textContent.Contains("{{"))
            {
                // Has static text content - use text-based selector
                propertyName = ToPascalCase(textContent) + GetElementTypeSuffix(elementType);
                selectorValue = $"{elementType}:has-text(\"{textContent}\")";
                strategy = SelectorStrategy.Text;
            }
            else
            {
                // No static text - use handler name to derive property name
                // Convert handler like "onClick" -> "Click", "onSubmit" -> "Submit"
                var cleanHandlerName = handlerName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    ? handlerName[2..]
                    : handlerName;
                propertyName = ToPascalCase(cleanHandlerName) + GetElementTypeSuffix(elementType);

                // Use element type selector for non-text elements
                if (elementType == "button")
                {
                    selectorValue = "button";
                    strategy = SelectorStrategy.Role;
                }
                else
                {
                    selectorValue = elementType;
                    strategy = SelectorStrategy.Css;
                }
                textContent = null;
            }

            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                continue;
            }

            selectors.Add(new ElementSelector
            {
                ElementType = elementType,
                Strategy = strategy,
                SelectorValue = selectorValue,
                PropertyName = propertyName,
                TextContent = textContent,
                HasClickHandler = true,
                ClickHandlerName = handlerName
            });
        }

        // Parse elements with routerLink
        foreach (Match match in RouterLinkRegex().Matches(templateContent))
        {
            var route = match.Groups[1].Value;
            var elementType = ExtractElementType(templateContent, match.Index);
            var textContent = ExtractTextContent(templateContent, match.Index);

            if (string.IsNullOrWhiteSpace(textContent) || textContent.Contains("{{"))
            {
                continue;
            }

            var propertyName = ToPascalCase(textContent) + "Link";
            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                continue;
            }

            selectors.Add(new ElementSelector
            {
                ElementType = elementType,
                Strategy = SelectorStrategy.Text,
                SelectorValue = $"[routerLink]:has-text(\"{textContent}\")",
                PropertyName = propertyName,
                TextContent = textContent,
                IsLink = true
            });
        }

        // Parse Angular Material buttons (mat-button, mat-raised-button, etc.)
        foreach (Match match in MatButtonRegex().Matches(templateContent))
        {
            var fullMatch = match.Value;
            var textContent = ExtractMatButtonText(fullMatch);

            if (string.IsNullOrWhiteSpace(textContent) || textContent.Contains("{{"))
            {
                continue;
            }

            var propertyName = ToPascalCase(textContent) + "Button";
            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                continue;
            }

            var hasClick = fullMatch.Contains("(click)");
            var clickHandler = hasClick ? ExtractClickHandler(fullMatch) : null;

            selectors.Add(new ElementSelector
            {
                ElementType = "button",
                Strategy = SelectorStrategy.Role,
                SelectorValue = $"button:has-text(\"{textContent}\")",
                PropertyName = propertyName,
                TextContent = textContent,
                IsMaterialComponent = true,
                HasClickHandler = hasClick,
                ClickHandlerName = clickHandler
            });
        }

        // Parse Angular Material form fields
        foreach (Match match in MatFormFieldRegex().Matches(templateContent))
        {
            var fullMatch = match.Value;
            var labelMatch = MatLabelRegex().Match(fullMatch);
            if (!labelMatch.Success)
            {
                continue;
            }

            var label = labelMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(label) || label.Contains("{{"))
            {
                continue;
            }

            var propertyName = ToPascalCase(label) + "Field";
            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                continue;
            }

            selectors.Add(new ElementSelector
            {
                ElementType = "mat-form-field",
                Strategy = SelectorStrategy.Label,
                SelectorValue = $"mat-form-field:has(mat-label:has-text(\"{label}\"))",
                PropertyName = propertyName,
                TextContent = label,
                IsMaterialComponent = true
            });
        }

        // Parse tables and mat-tables
        foreach (Match match in TableRegex().Matches(templateContent))
        {
            var fullMatch = match.Value;
            var isMatTable = fullMatch.Contains("mat-table") || fullMatch.Contains("[matSort]");
            var tableId = ExtractAttribute(fullMatch, "id") ?? ExtractAttribute(fullMatch, "data-testid");

            var propertyName = !string.IsNullOrEmpty(tableId)
                ? ToPascalCase(tableId) + "Table"
                : isMatTable ? "DataTable" : "Table";

            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                propertyName = propertyName + selectors.Count(s => s.IsTable);
            }

            var selectorValue = isMatTable
                ? "mat-table, table[mat-table], [mat-table]"
                : !string.IsNullOrEmpty(tableId)
                    ? $"#{tableId}"
                    : "table";

            selectors.Add(new ElementSelector
            {
                ElementType = isMatTable ? "mat-table" : "table",
                Strategy = !string.IsNullOrEmpty(tableId) ? SelectorStrategy.Id : SelectorStrategy.Css,
                SelectorValue = selectorValue,
                PropertyName = propertyName,
                IsTable = true,
                IsMaterialComponent = isMatTable
            });
        }

        // Parse elements with text content (custom components like <m-foo>Text</m-foo>)
        foreach (Match match in CustomElementWithTextRegex().Matches(templateContent))
        {
            var tagName = match.Groups[1].Value;
            var textContent = match.Groups[2].Value.Trim();

            // Skip standard HTML elements and elements we've already handled
            if (IsStandardHtmlElement(tagName) || string.IsNullOrWhiteSpace(textContent) || textContent.Contains("{{"))
            {
                continue;
            }

            var propertyName = ToPascalCase(textContent) + GetElementTypeSuffix(tagName);
            if (selectors.Any(s => s.PropertyName == propertyName))
            {
                continue;
            }

            selectors.Add(new ElementSelector
            {
                ElementType = tagName,
                Strategy = SelectorStrategy.Text,
                SelectorValue = $"{tagName}:has-text(\"{textContent}\")",
                PropertyName = propertyName,
                TextContent = textContent
            });
        }

        return selectors;
    }

    private static string? ExtractTextContent(string content, int position)
    {
        // Find the closing > of the opening tag
        var tagEnd = content.IndexOf('>', position);
        if (tagEnd < 0) return null;

        // Find the next < which starts the closing tag or a child element
        var textEnd = content.IndexOf('<', tagEnd + 1);
        if (textEnd < 0) return null;

        var text = content[(tagEnd + 1)..textEnd].Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ExtractClickHandler(string elementContent)
    {
        var match = ClickHandlerRegex().Match(elementContent);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractAttribute(string elementContent, string attributeName)
    {
        var pattern = $@"{attributeName}\s*=\s*['""]([^'""]+)['""]";
        var match = Regex.Match(elementContent, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ExtractMatButtonText(string buttonElement)
    {
        // Try to find text between > and <
        var match = Regex.Match(buttonElement, @">([^<]+)<");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return string.Empty;
    }

    private static string GetElementTypeSuffix(string elementType)
    {
        return elementType.ToLowerInvariant() switch
        {
            "button" or "mat-button" => "Button",
            "a" => "Link",
            "input" => "Input",
            "table" or "mat-table" => "Table",
            _ when elementType.StartsWith("mat-", StringComparison.OrdinalIgnoreCase) => "Element",
            _ => "Element"
        };
    }

    private static bool IsStandardHtmlElement(string tagName)
    {
        var standardElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "div", "span", "p", "h1", "h2", "h3", "h4", "h5", "h6",
            "a", "button", "input", "form", "label", "select", "option",
            "table", "tr", "td", "th", "thead", "tbody", "tfoot",
            "ul", "ol", "li", "nav", "header", "footer", "main", "section",
            "article", "aside", "img", "video", "audio", "canvas", "svg"
        };
        return standardElements.Contains(tagName);
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

    [GeneratedRegex(@"\(click\)\s*=\s*['""](\w+)\([^)]*\)['""]")]
    private static partial Regex ClickHandlerRegex();

    [GeneratedRegex(@"routerLink\s*=\s*['""]([^'""]+)['""]|\[routerLink\]\s*=\s*['""]([^'""]+)['""]")]
    private static partial Regex RouterLinkRegex();

    [GeneratedRegex(@"<button[^>]*(?:mat-button|mat-raised-button|mat-flat-button|mat-stroked-button|mat-icon-button|mat-fab|mat-mini-fab)[^>]*>([^<]*(?:<[^/][^>]*>[^<]*</[^>]+>)*[^<]*)</button>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MatButtonRegex();

    [GeneratedRegex(@"<mat-form-field[^>]*>.*?</mat-form-field>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MatFormFieldRegex();

    [GeneratedRegex(@"<mat-label[^>]*>([^<]+)</mat-label>", RegexOptions.IgnoreCase)]
    private static partial Regex MatLabelRegex();

    [GeneratedRegex(@"<(?:table|mat-table|\w+\s+mat-table)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex TableRegex();

    [GeneratedRegex(@"<([\w-]+)[^>]*>([^<]+)</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex CustomElementWithTextRegex();

    [GeneratedRegex(@"template\s*:\s*`([^`]*)`", RegexOptions.Singleline)]
    private static partial Regex InlineTemplateBacktickRegex();

    [GeneratedRegex(@"template\s*:\s*'([^']*)'", RegexOptions.Singleline)]
    private static partial Regex InlineTemplateSingleQuoteRegex();

    [GeneratedRegex(@"template\s*:\s*""([^""]*)""", RegexOptions.Singleline)]
    private static partial Regex InlineTemplateDoubleQuoteRegex();

    [GeneratedRegex(@"<input[^>]*type\s*=\s*['""]([^'""]+)['""][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputWithTypeRegex();
}
