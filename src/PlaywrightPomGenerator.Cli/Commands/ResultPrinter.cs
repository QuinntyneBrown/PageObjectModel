using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Shared console output for command handlers. Detailed analysis warnings flow
/// through the generation result's Warnings block; this prints the one-line engine
/// banner so users always know which analysis path produced their files, and the
/// error/fix pair when a command fails.
/// </summary>
public static class ResultPrinter
{
    /// <summary>
    /// Prints the analysis engine banner for a project, when a report is available.
    /// </summary>
    /// <param name="report">The project's analysis report.</param>
    /// <param name="projectName">When set, prefixes the banner with the project name (workspace runs).</param>
    public static void PrintAnalysisEngine(AnalysisReport? report, string? projectName = null)
    {
        if (report is null)
        {
            return;
        }

        var banner = report.EngineUsed switch
        {
            AnalysisEngineUsed.Ast =>
                $"Analysis engine: AST (typescript {report.TypeScriptVersion ?? "unknown"}, @angular/compiler {report.AngularCompilerVersion ?? "unknown"})",
            AnalysisEngineUsed.AstWithRegexTemplates =>
                $"Analysis engine: AST + regex templates (@angular/compiler not found in node_modules — template analysis fell back to regex; typescript {report.TypeScriptVersion ?? "unknown"})",
            _ => report.FallbackReason is not null
                ? $"Analysis engine: regex ({report.FallbackReason})"
                : "Analysis engine: regex"
        };
        Console.WriteLine(projectName is null ? banner : $"[{projectName}] {banner}");
    }

    /// <summary>
    /// Prints one engine banner per workspace project.
    /// </summary>
    public static void PrintAnalysisEngines(IEnumerable<AngularProjectInfo> projects)
    {
        foreach (var project in projects)
        {
            PrintAnalysisEngine(project.Analysis, project.Name);
        }
    }

    /// <summary>
    /// Prints a command failure to stderr. Sidecar failures get a "Fix:" line with the
    /// remediation for their reason and, for commands that can fall back to regex
    /// analysis, a hint that <c>--engine auto</c> avoids the hard error.
    /// </summary>
    /// <param name="ex">The exception that ended the command.</param>
    /// <param name="supportsRegexFallback">
    /// True for analysis commands (app, workspace, lib, component, artifacts, remote);
    /// false for commands that always need the sidecar (bridge) or never use it.
    /// </param>
    public static void PrintError(Exception ex, bool supportsRegexFallback)
    {
        ArgumentNullException.ThrowIfNull(ex);

        Console.Error.WriteLine($"Error: {ex.Message}");
        if (ex is SidecarUnavailableException sidecar)
        {
            Console.Error.WriteLine($"Fix: {sidecar.Remediation}");
            if (supportsRegexFallback)
            {
                Console.Error.WriteLine("Hint: with --engine auto (the default) the tool falls back to regex analysis instead of failing.");
            }
        }
    }
}
