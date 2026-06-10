using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// <see cref="IAstProjectAnalyzer"/> implementation invoking the sidecar's
/// analyzeProject method and deserializing the schemaVersion 1 response into
/// the wire DTOs.
/// </summary>
public sealed class AstProjectAnalyzer : IAstProjectAnalyzer
{
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISidecarTransport _transport;
    private readonly ILogger<AstProjectAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AstProjectAnalyzer"/> class.
    /// </summary>
    /// <param name="transport">The sidecar transport.</param>
    /// <param name="logger">The logger.</param>
    public AstProjectAnalyzer(ISidecarTransport transport, ILogger<AstProjectAnalyzer> logger)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AstProjectAnalysis> AnalyzeProjectAsync(
        string rootPath,
        IReadOnlyList<AstProjectTarget> projects,
        bool includeTemplateContent = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(projects);

        var fullRoot = Path.GetFullPath(rootPath);

        // The `root` property name is load-bearing: the transport points NODE_PATH at it.
        var parameters = new
        {
            root = fullRoot,
            projects = projects.Select(p => new
            {
                name = p.Name,
                sourceRoot = Path.GetFullPath(p.SourceRoot),
                prefix = p.Prefix
            }).ToArray(),
            options = new { includeTemplateContent }
        };

        var result = await _transport.InvokeAsync("analyzeProject", parameters, cancellationToken).ConfigureAwait(false);

        AstProjectAnalysis? analysis;
        try
        {
            analysis = result.Deserialize<AstProjectAnalysis>(SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Could not parse the sidecar analyzeProject response: {ex.Message}", ex);
        }

        if (analysis is null)
        {
            throw new InvalidOperationException("The sidecar analyzeProject response was empty.");
        }
        if (analysis.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported analyzeProject schema version {analysis.SchemaVersion} (expected {SupportedSchemaVersion}). " +
                "The sidecar and the tool are out of sync — reinstall the tool.");
        }

        foreach (var warning in analysis.Warnings)
        {
            _logger.LogWarning("Sidecar: {Warning}", warning);
        }
        foreach (var project in analysis.Projects)
        {
            foreach (var warning in project.Warnings)
            {
                _logger.LogWarning("Sidecar [{Project}]: {Warning}", project.Name, warning);
            }
        }

        return analysis;
    }
}
