using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// <see cref="IPackageInspector"/> implementation reading package.json and
/// angular.json with proper JSON parsing (no regex).
/// </summary>
public sealed class PackageInspector : IPackageInspector
{
    private static readonly string[] RecognizedUiLibraries =
    [
        "@angular/material",
        "@angular/cdk",
        "primeng",
        "ng-zorro-antd",
        "@ionic/angular",
        "@ng-bootstrap/ng-bootstrap",
        "ngx-bootstrap"
    ];

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PackageInspector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageInspector"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger.</param>
    public PackageInspector(IFileSystem fileSystem, ILogger<PackageInspector> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PackageReport> InspectAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        var packageJsonPath = _fileSystem.CombinePath(rootPath, "package.json");
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        string? readPackageJsonPath = null;

        if (_fileSystem.FileExists(packageJsonPath))
        {
            try
            {
                var content = await _fileSystem.ReadAllTextAsync(packageJsonPath, cancellationToken).ConfigureAwait(false);
                using var document = JsonDocument.Parse(content);
                CollectDependencies(document.RootElement, "dependencies", dependencies);
                CollectDependencies(document.RootElement, "devDependencies", dependencies);
                readPackageJsonPath = packageJsonPath;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Could not parse {Path}: {Message}", packageJsonPath, ex.Message);
            }
        }

        var uiLibraries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var library in RecognizedUiLibraries)
        {
            if (dependencies.TryGetValue(library, out var version))
            {
                uiLibraries[library] = version;
            }
        }

        return new PackageReport
        {
            AngularCoreVersion = dependencies.GetValueOrDefault("@angular/core"),
            AngularMaterialVersion = dependencies.GetValueOrDefault("@angular/material"),
            AngularCdkVersion = dependencies.GetValueOrDefault("@angular/cdk"),
            TypeScriptVersion = dependencies.GetValueOrDefault("typescript"),
            UiLibraries = uiLibraries,
            Prefixes = await ReadPrefixesAsync(rootPath, cancellationToken).ConfigureAwait(false),
            NodeModulesPresent = _fileSystem.DirectoryExists(_fileSystem.CombinePath(rootPath, "node_modules")),
            PackageJsonPath = readPackageJsonPath
        };
    }

    private static void CollectDependencies(JsonElement root, string sectionName, Dictionary<string, string> into)
    {
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        foreach (var dependency in section.EnumerateObject())
        {
            if (dependency.Value.ValueKind == JsonValueKind.String && !into.ContainsKey(dependency.Name))
            {
                into[dependency.Name] = dependency.Value.GetString() ?? "";
            }
        }
    }

    private async Task<IReadOnlyList<string>> ReadPrefixesAsync(string rootPath, CancellationToken cancellationToken)
    {
        var angularJsonPath = _fileSystem.CombinePath(rootPath, "angular.json");
        if (!_fileSystem.FileExists(angularJsonPath))
        {
            return [];
        }

        var prefixes = new List<string>();
        try
        {
            var content = await _fileSystem.ReadAllTextAsync(angularJsonPath, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            AddSchematicsPrefix(root, prefixes);

            if (root.TryGetProperty("projects", out var projects) && projects.ValueKind == JsonValueKind.Object)
            {
                foreach (var project in projects.EnumerateObject())
                {
                    if (project.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    if (project.Value.TryGetProperty("prefix", out var prefix) && prefix.ValueKind == JsonValueKind.String)
                    {
                        AddPrefix(prefixes, prefix.GetString());
                    }
                    AddSchematicsPrefix(project.Value, prefixes);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Could not parse {Path}: {Message}", angularJsonPath, ex.Message);
        }
        return prefixes;
    }

    private static void AddSchematicsPrefix(JsonElement scope, List<string> prefixes)
    {
        if (scope.TryGetProperty("schematics", out var schematics) && schematics.ValueKind == JsonValueKind.Object
            && schematics.TryGetProperty("@schematics/angular:component", out var component) && component.ValueKind == JsonValueKind.Object
            && component.TryGetProperty("prefix", out var prefix) && prefix.ValueKind == JsonValueKind.String)
        {
            AddPrefix(prefixes, prefix.GetString());
        }
    }

    private static void AddPrefix(List<string> prefixes, string? prefix)
    {
        if (!string.IsNullOrWhiteSpace(prefix) && !prefixes.Contains(prefix, StringComparer.OrdinalIgnoreCase))
        {
            prefixes.Add(prefix);
        }
    }
}
