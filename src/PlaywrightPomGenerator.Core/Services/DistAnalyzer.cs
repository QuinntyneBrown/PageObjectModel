using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// <see cref="IDistAnalyzer"/> implementation over <see cref="IFileSystem"/>.
/// Extracts the &lt;base href&gt; with a single-tag regex (no HTML parser
/// dependency) and confirms prerendered routes from nested index.html files.
/// A route's absence here means nothing — prerendering is selective.
/// </summary>
public sealed partial class DistAnalyzer : IDistAnalyzer
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<DistAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistAnalyzer"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger.</param>
    public DistAnalyzer(IFileSystem fileSystem, ILogger<DistAnalyzer> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DistAnalysis?> AnalyzeAsync(
        string projectRoot,
        string projectName,
        string? explicitDistPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        ArgumentNullException.ThrowIfNull(projectName);

        var distPath = ResolveDistPath(projectRoot, projectName, explicitDistPath);
        if (distPath is null)
        {
            _logger.LogInformation("No dist output found for {ProjectName} — skipping dist analysis", projectName);
            return null;
        }

        string? baseHref = null;
        var rootIndex = _fileSystem.CombinePath(distPath, "index.html");
        if (_fileSystem.FileExists(rootIndex))
        {
            try
            {
                var html = await _fileSystem.ReadAllTextAsync(rootIndex, cancellationToken).ConfigureAwait(false);
                var match = BaseHrefRegex().Match(html);
                if (match.Success)
                {
                    baseHref = match.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read {IndexPath}", rootIndex);
            }
        }

        var prerenderedRoutes = FindPrerenderedRoutes(distPath);

        return new DistAnalysis
        {
            DistPath = distPath,
            BaseHref = baseHref,
            PrerenderedRoutes = prerenderedRoutes
        };
    }

    private string? ResolveDistPath(string projectRoot, string projectName, string? explicitDistPath)
    {
        if (explicitDistPath is not null)
        {
            var full = _fileSystem.GetFullPath(explicitDistPath);
            if (_fileSystem.DirectoryExists(full))
            {
                return full;
            }
            _logger.LogWarning("The dist path '{DistPath}' does not exist", explicitDistPath);
            return null;
        }

        // Angular 17+ application builder, then the classic layouts.
        string[] candidates =
        [
            _fileSystem.CombinePath(projectRoot, "dist", projectName, "browser"),
            _fileSystem.CombinePath(projectRoot, "dist", projectName),
            _fileSystem.CombinePath(projectRoot, "dist")
        ];
        foreach (var candidate in candidates)
        {
            if (_fileSystem.FileExists(_fileSystem.CombinePath(candidate, "index.html")))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// Every nested index.html marks a prerendered route ("/about" from
    /// about/index.html).
    /// </summary>
    private List<string> FindPrerenderedRoutes(string distPath)
    {
        var routes = new List<string>();
        try
        {
            foreach (var indexFile in _fileSystem.GetFiles(distPath, "index.html", recursive: true))
            {
                var directory = _fileSystem.GetDirectoryName(indexFile);
                if (directory is null)
                {
                    continue;
                }
                var relative = Path.GetRelativePath(distPath, directory).Replace('\\', '/');
                if (relative == "." || relative.Length == 0)
                {
                    continue; // the app shell itself
                }
                routes.Add("/" + relative);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not scan {DistPath} for prerendered routes", distPath);
        }
        routes.Sort(StringComparer.Ordinal);
        return routes;
    }

    [GeneratedRegex(@"<base\s+href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex BaseHrefRegex();
}
