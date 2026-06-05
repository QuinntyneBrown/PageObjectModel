namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Locates the Node TypeScript AST sidecar script (sidecar.js) at runtime.
/// </summary>
public static class SidecarLocator
{
    /// <summary>
    /// Resolves the path to sidecar.js, checking (in order): the <c>POMGEN_SIDECAR</c> environment
    /// variable, a <c>sidecar/</c> folder shipped next to the tool, and the source location when
    /// running from the repository.
    /// </summary>
    /// <returns>The best path to sidecar.js (which may not exist if the sidecar is not installed).</returns>
    public static string Locate()
    {
        var fromEnv = Environment.GetEnvironmentVariable("POMGEN_SIDECAR");
        if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        var baseDir = AppContext.BaseDirectory;
        var shipped = Path.Combine(baseDir, "sidecar", "sidecar.js");
        if (File.Exists(shipped))
        {
            return shipped;
        }

        // Walk up the directory tree to find the source sidecar (dev / test scenarios).
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PlaywrightPomGenerator.Sidecar.Node", "sidecar.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        // Best-effort default; the transport surfaces a clear "not found" error if it is missing.
        return shipped;
    }
}
