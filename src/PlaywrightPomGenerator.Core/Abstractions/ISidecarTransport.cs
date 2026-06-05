using System.Text.Json;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Low-level transport to the TypeScript AST sidecar. Sends a single JSON-RPC request and returns
/// the <c>result</c> payload. Implementations own process/IPC concerns; this seam keeps the
/// analyzer unit-testable with a fake transport.
/// </summary>
public interface ISidecarTransport
{
    /// <summary>
    /// Invokes a sidecar method and returns its JSON-RPC <c>result</c>.
    /// </summary>
    /// <param name="method">The sidecar method name (e.g. "discoverInjectionTokens").</param>
    /// <param name="parameters">The parameters object, serialized to the request's <c>params</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <c>result</c> element from the sidecar response.</returns>
    Task<JsonElement> InvokeAsync(string method, object parameters, CancellationToken cancellationToken = default);
}
