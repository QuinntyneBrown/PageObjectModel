using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// AST engine orchestration for <see cref="AngularAnalyzer"/>: runs the sidecar's
/// batched analyzeProject (engine selection + fallback) and maps its wire DTOs
/// onto the analysis models. The regex path in the main file remains both the
/// whole-run fallback and the per-component template fallback.
/// </summary>
public sealed partial class AngularAnalyzer
{
    private sealed record EngineOutcome(AstProjectAnalysis? Ast, string? FallbackReason);

    /// <summary>
    /// Runs AST analysis once per top-level analyze call, honoring the engine option:
    /// Regex skips the sidecar; Auto falls back to regex on failure; Ast rethrows.
    /// </summary>
    private async Task<EngineOutcome> RunEngineAsync(
        string rootPath,
        IReadOnlyList<AstProjectTarget> targets,
        CancellationToken cancellationToken)
    {
        var requested = _options.AnalysisEngine;
        if (requested == AnalysisEngine.Regex)
        {
            return new EngineOutcome(null, "--engine regex");
        }

        try
        {
            var ast = await _astProjectAnalyzer
                .AnalyzeProjectAsync(rootPath, targets, _options.DebugMode, cancellationToken)
                .ConfigureAwait(false);
            return new EngineOutcome(ast, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (requested == AnalysisEngine.Auto
            && ex is SidecarUnavailableException or InvalidOperationException)
        {
            var reason = FallbackReasonText(ex);
            _logger.LogWarning("AST analysis unavailable ({Reason}); falling back to regex analysis.", reason);
            return new EngineOutcome(null, reason);
        }
    }

    private static string FallbackReasonText(Exception ex) => ex switch
    {
        SidecarUnavailableException sidecar => sidecar.Reason switch
        {
            SidecarUnavailableReason.NodeMissing =>
                "Node.js not found — install Node.js 18+ or set POMGEN_NODE to enable AST analysis",
            SidecarUnavailableReason.TypeScriptMissing =>
                "typescript not resolvable from the analyzed project's node_modules — run 'npm install' to enable AST analysis",
            SidecarUnavailableReason.SidecarMissing =>
                "sidecar not found — set POMGEN_SIDECAR or reinstall the tool",
            _ => sidecar.Message
        },
        _ => ex.Message
    };

    /// <summary>
    /// Produces components, routes, and the analysis report for one project from
    /// the engine outcome (AST result when available, regex otherwise).
    /// </summary>
    private async Task<(List<AngularComponentInfo> Components, List<RouteInfo> Routes, AnalysisReport Report)>
        BuildProjectAnalysisAsync(
            string projectName,
            string sourcePath,
            EngineOutcome engine,
            PackageReport packages,
            CancellationToken cancellationToken)
    {
        if (engine.Ast is { } ast)
        {
            var astProject = ast.Projects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.Ordinal));
            if (astProject is not null)
            {
                return await MapAstProjectAsync(astProject, ast, packages, cancellationToken).ConfigureAwait(false);
            }
            _logger.LogWarning("AST analysis returned no result for project {Project}; using regex analysis for it.", projectName);
        }

        var components = await AnalyzeComponentsAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var routes = await AnalyzeRoutesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var report = new AnalysisReport
        {
            EngineRequested = _options.AnalysisEngine,
            EngineUsed = AnalysisEngineUsed.Regex,
            FallbackReason = engine.FallbackReason
        };
        return (components, routes, report);
    }

    private async Task<(List<AngularComponentInfo> Components, List<RouteInfo> Routes, AnalysisReport Report)>
        MapAstProjectAsync(
            AstProjectResult astProject,
            AstProjectAnalysis analysis,
            PackageReport packages,
            CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        warnings.AddRange(analysis.Warnings);
        warnings.AddRange(astProject.Warnings);

        var routeIndex = BuildComponentRouteIndex(astProject.Routes);

        var components = new List<AngularComponentInfo>();
        foreach (var astComponent in astProject.Components)
        {
            cancellationToken.ThrowIfCancellationRequested();
            components.Add(await MapAstComponentAsync(astComponent, routeIndex, packages, warnings, cancellationToken)
                .ConfigureAwait(false));
        }

        var routes = astProject.Routes?.Tree.Select(MapRouteNode).ToList() ?? [];

        var report = new AnalysisReport
        {
            EngineRequested = _options.AnalysisEngine,
            EngineUsed = analysis.Engine?.AngularCompiler is not null
                ? AnalysisEngineUsed.Ast
                : AnalysisEngineUsed.AstWithRegexTemplates,
            TypeScriptVersion = analysis.Engine?.TypeScript,
            AngularCompilerVersion = analysis.Engine?.AngularCompiler,
            Warnings = warnings
        };
        return (components, routes, report);
    }

    private async Task<AngularComponentInfo> MapAstComponentAsync(
        AstComponent ast,
        Dictionary<string, AstComponentRoute> routeIndex,
        PackageReport packages,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        List<ElementSelector> selectors = [];
        var templateContent = ast.TemplateContent;

        if (ast.TemplateParsed && ast.Template is not null)
        {
            var dialogOpensByHandler = ast.DialogOpens
                .Where(d => d.Handler is not null && d.ComponentName is not null)
                .GroupBy(d => d.Handler!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().ComponentName!, StringComparer.Ordinal);
            selectors = SelectorNaming.MapSelectors(ast.Template.Elements, dialogOpensByHandler);
        }
        else if (ast.TemplateSource is "external" or "inline")
        {
            var fallbackTemplate = await ReadTemplateForRegexFallbackAsync(ast, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(fallbackTemplate))
            {
                selectors = ParseTemplateSelectors(fallbackTemplate);
                templateContent ??= fallbackTemplate;
                var detail = ast.TemplateErrors.Count > 0 ? $" ({ast.TemplateErrors[0]})" : "";
                warnings.Add($"{ast.ClassName}: template analyzed via regex fallback{detail}");
            }
        }

        var routeMatch = routeIndex.GetValueOrDefault(RouteIndexKey(ast.FilePath, ast.ClassName));
        var routePaths = routeMatch?.FullPaths.Select(TrimRoutePath).ToList() ?? [];
        var primaryRoute = PickPrimaryRoute(routePaths);
        var routeEvidence = routeMatch is not null;

        return new AngularComponentInfo
        {
            Name = ast.ClassName,
            Selector = ast.Selector ?? "unknown",
            FilePath = ast.FilePath,
            TemplatePath = ast.TemplateUrl,
            TemplateContent = templateContent,
            Selectors = selectors,
            Inputs = ast.Inputs.Select(p => p.Name).ToList(),
            Outputs = ast.Outputs.Select(p => p.Name).ToList(),
            InputsDetailed = ast.Inputs.Select(MapPort).ToList(),
            OutputsDetailed = ast.Outputs.Select(MapPort).ToList(),
            IsStandalone = ast.Standalone ?? DefaultStandalone(packages),
            ChildComponents = ast.ChildComponents.Select(MapChildComponent).ToList(),
            Forms = ast.Template?.Forms.Select(MapForm).ToList() ?? [],
            RoutePath = primaryRoute,
            RoutePaths = routePaths,
            RouteParams = ExtractRouteParams(primaryRoute),
            RouteEvidence = routeEvidence,
            TitleFromRoute = routeMatch?.Titles.FirstOrDefault(),
            IsRoutable = routeEvidence || IsRoutableComponent(ast.FilePath, ast.ClassName)
        };
    }

    private async Task<string?> ReadTemplateForRegexFallbackAsync(AstComponent ast, CancellationToken cancellationToken)
    {
        try
        {
            if (ast.TemplateUrl is not null && _fileSystem.FileExists(ast.TemplateUrl))
            {
                return await _fileSystem.ReadAllTextAsync(ast.TemplateUrl, cancellationToken).ConfigureAwait(false);
            }
            if (ast.TemplateSource == "inline" && _fileSystem.FileExists(ast.FilePath))
            {
                var content = await _fileSystem.ReadAllTextAsync(ast.FilePath, cancellationToken).ConfigureAwait(false);
                return ExtractInlineTemplate(content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the template of {Component} for regex fallback", ast.ClassName);
        }
        return null;
    }

    // --- mapping helpers -------------------------------------------------------

    private static Dictionary<string, AstComponentRoute> BuildComponentRouteIndex(AstRouteAnalysis? routes)
    {
        var index = new Dictionary<string, AstComponentRoute>(StringComparer.OrdinalIgnoreCase);
        foreach (var componentRoute in routes?.ComponentRoutes ?? [])
        {
            index[RouteIndexKey(componentRoute.ComponentFilePath, componentRoute.ComponentClassName)] = componentRoute;
        }
        return index;
    }

    private static string RouteIndexKey(string filePath, string className) =>
        filePath.Replace('\\', '/') + "::" + className;

    private static string TrimRoutePath(string fullPath) => fullPath.TrimStart('/');

    /// <summary>
    /// The primary route is the shortest parameter-free path, else the first found.
    /// </summary>
    private static string? PickPrimaryRoute(IReadOnlyList<string> routePaths)
    {
        if (routePaths.Count == 0)
        {
            return null;
        }
        var parameterFree = routePaths
            .Where(p => !p.Contains(':'))
            .OrderBy(p => p.Count(c => c == '/'))
            .ThenBy(p => p.Length)
            .FirstOrDefault();
        return parameterFree ?? routePaths[0];
    }

    private static IReadOnlyList<string> ExtractRouteParams(string? routePath)
    {
        if (string.IsNullOrEmpty(routePath))
        {
            return [];
        }
        return routePath
            .Split('/')
            .Where(s => s.StartsWith(':'))
            .Select(s => RouteParamSanitizeRegex().Replace(s[1..], ""))
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static bool? DefaultStandalone(PackageReport packages)
    {
        var major = ParseMajorVersion(packages.AngularCoreVersion);
        // Angular 19 flipped the default of `standalone` to true.
        return major is null ? null : major >= 19;
    }

    private static int? ParseMajorVersion(string? version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }
        var match = LeadingNumberRegex().Match(version);
        return match.Success && int.TryParse(match.Groups[1].Value, out var major) ? major : null;
    }

    private static ComponentPortInfo MapPort(AstPort port) => new()
    {
        Name = port.Name,
        Alias = port.Alias,
        Required = port.Required,
        Kind = port.Kind switch
        {
            "signal" => ComponentPortKind.Signal,
            "model" => ComponentPortKind.Model,
            _ => ComponentPortKind.Decorator
        }
    };

    private static ChildComponentRef MapChildComponent(AstChildComponent child) => new()
    {
        Selector = child.Selector,
        ComponentName = child.ComponentClassName,
        ComponentFilePath = child.ComponentFilePath,
        Count = child.Count,
        IsConditional = child.Conditional,
        IsRepeated = child.Repeated,
        Library = child.Library
    };

    private static FormInfo MapForm(AstForm form) => new()
    {
        FormGroupName = form.FormGroup,
        SubmitHandlerName = form.SubmitHandler,
        Controls = form.Controls.Select(c => new FormControlInfo
        {
            ControlName = c.Name,
            ControlType = NormalizeFormControlType(
                SelectorNaming.DeriveControlType(c.Widget, c.Tag, c.InputType, opensDialog: false))
        }).ToList()
    };

    private static ControlType NormalizeFormControlType(ControlType controlType) =>
        controlType == ControlType.None ? ControlType.TextInput : controlType;

    private static RouteInfo MapRouteNode(AstRouteNode node) => new()
    {
        Path = node.Path ?? "",
        Component = node.Component,
        RedirectTo = node.RedirectTo,
        Children = node.Children.Select(MapRouteNode).ToList(),
        IsLazyLoaded = node.IsLazy,
        FullPath = node.FullPath,
        ComponentFilePath = node.ComponentFilePath,
        PathParameters = node.PathParams,
        IsWildcard = node.Wildcard,
        PathMatch = node.PathMatch,
        Title = node.Title,
        Outlet = node.Outlet,
        Guards = node.Guards,
        DataKeys = node.DataKeys,
        SourceFilePath = node.SourceFile
    };

    /// <summary>
    /// Walks up from a target directory to the nearest package.json (max 6 levels)
    /// so AST analysis of an arbitrary sub-path still resolves the workspace's
    /// node_modules and tsconfig.
    /// </summary>
    private string FindWorkspaceRoot(string startPath)
    {
        var current = startPath;
        for (var i = 0; i <= 6 && !string.IsNullOrEmpty(current); i++)
        {
            if (_fileSystem.FileExists(_fileSystem.CombinePath(current, "package.json")))
            {
                return current;
            }
            current = _fileSystem.GetDirectoryName(current);
        }
        return startPath;
    }

    [GeneratedRegex(@"[^\w]")]
    private static partial Regex RouteParamSanitizeRegex();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex LeadingNumberRegex();
}
