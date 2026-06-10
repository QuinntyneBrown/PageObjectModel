using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Shared console output for command handlers. Detailed analysis warnings flow
/// through the logger pipeline; this prints only the one-line engine banner so
/// users always know which analysis path produced their files.
/// </summary>
internal static class ResultPrinter
{
    /// <summary>
    /// Prints the analysis engine banner for a project, when a report is available.
    /// </summary>
    public static void PrintAnalysisEngine(AnalysisReport? report)
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
        Console.WriteLine(banner);
    }
}
