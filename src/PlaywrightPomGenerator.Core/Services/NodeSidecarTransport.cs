using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// <see cref="ISidecarTransport"/> backed by a Node process running the TypeScript AST sidecar.
/// Spawns <c>node sidecar.js</c>, exchanges a single newline-delimited JSON-RPC message over stdio,
/// and returns the result.
/// </summary>
public sealed class NodeSidecarTransport : ISidecarTransport
{
    private const int TypeScriptMissingExitCode = 2;

    private readonly string _nodeExecutable;
    private readonly string _sidecarScriptPath;
    private readonly ILogger<NodeSidecarTransport> _logger;
    private readonly TimeSpan? _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeSidecarTransport"/> class.
    /// </summary>
    /// <param name="nodeExecutable">The Node executable (e.g. "node").</param>
    /// <param name="sidecarScriptPath">The absolute path to sidecar.js.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeout">Optional per-invocation timeout; null disables it.</param>
    public NodeSidecarTransport(string nodeExecutable, string sidecarScriptPath, ILogger<NodeSidecarTransport> logger, TimeSpan? timeout = null)
    {
        _nodeExecutable = string.IsNullOrWhiteSpace(nodeExecutable) ? "node" : nodeExecutable;
        _sidecarScriptPath = sidecarScriptPath ?? throw new ArgumentNullException(nameof(sidecarScriptPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout;
    }

    /// <inheritdoc />
    public async Task<JsonElement> InvokeAsync(string method, object parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (!File.Exists(_sidecarScriptPath))
        {
            throw new SidecarUnavailableException(
                SidecarUnavailableReason.SidecarMissing,
                $"TypeScript sidecar not found at '{_sidecarScriptPath}'. Set POMGEN_SIDECAR or reinstall the tool.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_timeout is { } timeout && timeout > TimeSpan.Zero)
        {
            timeoutSource.CancelAfter(timeout);
        }
        var linkedToken = timeoutSource.Token;

        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
            @params = parameters
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = _nodeExecutable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_sidecarScriptPath) ?? Environment.CurrentDirectory
        };
        startInfo.ArgumentList.Add(_sidecarScriptPath);

        // Make the target workspace's own typescript resolvable as a fallback to a bundled copy.
        if (parameters?.GetType().GetProperty("root")?.GetValue(parameters) is string root && !string.IsNullOrEmpty(root))
        {
            startInfo.Environment["NODE_PATH"] = Path.Combine(root, "node_modules");
        }

        using var process = new Process { StartInfo = startInfo };
        var stderr = new StringBuilder();

        try
        {
            try
            {
                if (!process.Start())
                {
                    throw new SidecarUnavailableException(
                        SidecarUnavailableReason.NodeMissing,
                        $"Failed to start the Node sidecar via '{_nodeExecutable}'. Is Node.js installed and on PATH?");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new SidecarUnavailableException(
                    SidecarUnavailableReason.NodeMissing,
                    $"Failed to start the Node sidecar via '{_nodeExecutable}'. Is Node.js installed and on PATH?",
                    ex);
            }

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderr.AppendLine(e.Data);
                }
            };
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(request.AsMemory(), linkedToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(linkedToken).ConfigureAwait(false);
            process.StandardInput.Close();

            string? responseLine = null;
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(linkedToken).ConfigureAwait(false)) is not null)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                responseLine = line;
                break;
            }

            await process.WaitForExitAsync(linkedToken).ConfigureAwait(false);

            if (responseLine is null)
            {
                var detail = stderr.ToString().Trim();
                if (process.ExitCode == TypeScriptMissingExitCode)
                {
                    throw new SidecarUnavailableException(
                        SidecarUnavailableReason.TypeScriptMissing,
                        $"The sidecar could not resolve the 'typescript' package. {detail}".Trim());
                }
                throw new SidecarUnavailableException(
                    SidecarUnavailableReason.ProtocolError,
                    $"The Node sidecar returned no response (exit code {process.ExitCode}). {detail}".Trim());
            }

            using var document = JsonDocument.Parse(responseLine);
            var element = document.RootElement;

            if (element.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : "unknown error";
                throw new InvalidOperationException($"Sidecar error: {message}");
            }

            if (!element.TryGetProperty("result", out var result))
            {
                throw new InvalidOperationException("Sidecar response did not contain a 'result'.");
            }

            return result.Clone();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SidecarUnavailableException(
                SidecarUnavailableReason.ProtocolError,
                $"The Node sidecar timed out after {_timeout?.TotalSeconds:F0}s (method '{method}').");
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process already gone — nothing to clean up.
            }
        }
    }
}
