namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Selects how Angular source is analyzed.
/// </summary>
public enum AnalysisEngine
{
    /// <summary>
    /// Prefer AST analysis via the Node sidecar; fall back to regex analysis
    /// with a warning when Node, typescript, or the sidecar are unavailable.
    /// </summary>
    Auto,

    /// <summary>
    /// Require AST analysis; fail instead of degrading when it is unavailable.
    /// </summary>
    Ast,

    /// <summary>
    /// Use the regex/string analysis only (no Node required).
    /// </summary>
    Regex
}
