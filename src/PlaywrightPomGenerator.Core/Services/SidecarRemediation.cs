namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// The single source of "how to fix it" text for every <see cref="SidecarUnavailableReason"/>.
/// Used by the regex-fallback banner and by the hard-error path so both say the same thing.
/// </summary>
public static class SidecarRemediation
{
    /// <summary>
    /// Returns the one-line remediation for a sidecar failure category.
    /// </summary>
    public static string For(SidecarUnavailableReason reason) => reason switch
    {
        SidecarUnavailableReason.NodeMissing =>
            "Install Node.js 18+ or point POMGEN_NODE at the node executable.",
        SidecarUnavailableReason.TypeScriptMissing =>
            "Run 'npm install' in the analyzed workspace so typescript resolves from its node_modules.",
        SidecarUnavailableReason.SidecarMissing =>
            "Set POMGEN_SIDECAR to the sidecar.js path, or reinstall the tool (dotnet tool update -g PlaywrightPomGenerator.Cli).",
        SidecarUnavailableReason.Timeout =>
            "Raise Generator:SidecarTimeoutSeconds (env POMGEN_Generator__SidecarTimeoutSeconds; 0 disables the timeout) or analyze a smaller path.",
        _ =>
            "Re-run with --debug to see sidecar output; check that 'node --version' is 18+ and that the workspace's node_modules is complete."
    };

    /// <summary>
    /// Returns the short cause label used in the regex-fallback banner.
    /// </summary>
    public static string Cause(SidecarUnavailableReason reason) => reason switch
    {
        SidecarUnavailableReason.NodeMissing => "Node.js not found",
        SidecarUnavailableReason.TypeScriptMissing => "typescript not resolvable from the analyzed project's node_modules",
        SidecarUnavailableReason.SidecarMissing => "sidecar not found",
        SidecarUnavailableReason.Timeout => "sidecar timed out",
        _ => "sidecar protocol error"
    };
}
