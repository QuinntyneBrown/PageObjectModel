namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Thrown when the Node sidecar cannot run at all (as opposed to an RPC-level
/// error). Carries a machine-readable reason so the analyzer's Auto engine can
/// fall back to regex analysis with an actionable message.
/// </summary>
public sealed class SidecarUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SidecarUnavailableException"/> class.
    /// </summary>
    /// <param name="reason">The machine-readable failure category.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public SidecarUnavailableException(SidecarUnavailableReason reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the failure category.
    /// </summary>
    public SidecarUnavailableReason Reason { get; }
}

/// <summary>
/// Why the sidecar could not run.
/// </summary>
public enum SidecarUnavailableReason
{
    /// <summary>sidecar.js was not found on disk.</summary>
    SidecarMissing,

    /// <summary>The Node executable could not be started.</summary>
    NodeMissing,

    /// <summary>The sidecar could not resolve the typescript package (exit code 2).</summary>
    TypeScriptMissing,

    /// <summary>The sidecar produced no usable response.</summary>
    ProtocolError
}
