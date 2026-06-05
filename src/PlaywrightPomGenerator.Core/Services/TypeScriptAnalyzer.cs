using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// <see cref="ITypeScriptAnalyzer"/> that delegates discovery to the TypeScript AST sidecar and maps
/// the JSON response onto <see cref="InjectionTokenInterface"/> models.
/// </summary>
public sealed class TypeScriptAnalyzer : ITypeScriptAnalyzer
{
    private readonly ISidecarTransport _transport;
    private readonly ILogger<TypeScriptAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeScriptAnalyzer"/> class.
    /// </summary>
    /// <param name="transport">The sidecar transport.</param>
    /// <param name="logger">The logger.</param>
    public TypeScriptAnalyzer(ISidecarTransport transport, ILogger<TypeScriptAnalyzer> logger)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InjectionTokenInterface>> DiscoverInjectionTokenInterfacesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var fullPath = Path.GetFullPath(path);
        _logger.LogInformation("Discovering InjectionToken interfaces under {Path}", fullPath);

        var result = await _transport.InvokeAsync("discoverInjectionTokens", new { root = fullPath }, cancellationToken)
            .ConfigureAwait(false);

        if (result.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
        {
            foreach (var warning in warnings.EnumerateArray())
            {
                _logger.LogWarning("Sidecar: {Warning}", warning.GetString());
            }
        }

        var interfaces = new List<InjectionTokenInterface>();
        if (result.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Array)
        {
            foreach (var token in tokens.EnumerateArray())
            {
                interfaces.Add(MapInterface(token));
            }
        }

        _logger.LogInformation("Discovered {Count} InjectionToken interface(s)", interfaces.Count);
        return interfaces;
    }

    private static InjectionTokenInterface MapInterface(JsonElement token)
    {
        var members = new List<InterfaceMember>();
        if (token.TryGetProperty("members", out var memberArray) && memberArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var member in memberArray.EnumerateArray())
            {
                members.Add(new InterfaceMember
                {
                    Name = GetString(member, "name") ?? string.Empty,
                    IsMethod = GetBool(member, "isMethod"),
                    ParametersText = GetString(member, "parametersText") ?? string.Empty,
                    ParameterNames = GetStringArray(member, "parameterNames"),
                    ReturnType = GetString(member, "returnType") ?? "unknown",
                    IsObservable = GetBool(member, "isObservable"),
                    ReturnsVoid = GetBool(member, "returnsVoid")
                });
            }
        }

        return new InjectionTokenInterface
        {
            TokenName = GetString(token, "tokenName") ?? string.Empty,
            InterfaceName = GetString(token, "interfaceName") ?? string.Empty,
            Description = GetString(token, "description"),
            TokenFilePath = GetString(token, "tokenFile") ?? string.Empty,
            InterfaceFilePath = GetString(token, "interfaceFile"),
            Members = members
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
            {
                items.Add(s);
            }
        }
        return items;
    }
}
