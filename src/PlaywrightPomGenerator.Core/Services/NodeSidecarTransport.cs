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
    private readonly string _nodeExecutable;
    private readonly string _sidecarScriptPath;
    private readonly ILogger<NodeSidecarTransport> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeSidecarTransport"/> class.
    /// </summary>
    /// <param name="nodeExecutable">The Node executable (e.g. "node").</param>
    /// <param name="sidecarScriptPath">The absolute path to sidecar.js.</param>
    /// <param name="logger">The logger.</param>
    public NodeSidecarTransport(string nodeExecutable, string sidecarScriptPath, ILogger<NodeSidecarTransport> logger)
    {
        _nodeExecutable = string.IsNullOrWhiteSpace(nodeExecutable) ? "node" : nodeExecutable;
        _sidecarScriptPath = sidecarScriptPath ?? throw new ArgumentNullException(nameof(sidecarScriptPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<JsonElement> InvokeAsync(string method, object parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (!File.Exists(_sidecarScriptPath))
        {
            throw new FileNotFoundException(
                $"TypeScript sidecar not found at '{_sidecarScriptPath}'. Ensure the Node sidecar is installed " +
                "(run 'npm install' in the sidecar directory or ship it with the tool).",
                _sidecarScriptPath);
        }

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
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start the Node sidecar via '{_nodeExecutable}'. Is Node.js installed and on PATH?");
            }

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderr.AppendLine(e.Data);
                }
            };
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(request.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();

            string? responseLine = null;
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                responseLine = line;
                break;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (responseLine is null)
            {
                throw new InvalidOperationException(
                    $"The Node sidecar returned no response (exit code {process.ExitCode}). {stderr}".Trim());
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
