using FluentAssertions;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

/// <summary>
/// Console output of the shared result printer: sidecar failures come with a fix
/// line, ordinary failures do not, and workspace banners are printed per project.
/// </summary>
[Collection("Console")]
public sealed class ResultPrinterTests
{
    private static (string Out, string Err) Capture(Action action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
        return (outWriter.ToString(), errWriter.ToString());
    }

    [Fact]
    public void PrintError_SidecarFailureWithFallback_ShouldPrintErrorFixAndHint()
    {
        var ex = new SidecarUnavailableException(SidecarUnavailableReason.NodeMissing, "Failed to start node");

        var (_, err) = Capture(() => ResultPrinter.PrintError(ex, supportsRegexFallback: true));

        err.Should().Contain("Error: Failed to start node");
        err.Should().Contain("Fix: " + SidecarRemediation.For(SidecarUnavailableReason.NodeMissing));
        err.Should().Contain("--engine auto");
    }

    [Fact]
    public void PrintError_SidecarFailureWithoutFallback_ShouldOmitTheHint()
    {
        var ex = new SidecarUnavailableException(SidecarUnavailableReason.TypeScriptMissing, "no typescript");

        var (_, err) = Capture(() => ResultPrinter.PrintError(ex, supportsRegexFallback: false));

        err.Should().Contain("Fix: ");
        err.Should().NotContain("--engine auto");
    }

    [Fact]
    public void PrintError_OrdinaryException_ShouldPrintOnlyTheError()
    {
        var (_, err) = Capture(() => ResultPrinter.PrintError(new InvalidOperationException("boom"), supportsRegexFallback: true));

        err.Should().Be("Error: boom" + Environment.NewLine);
    }

    [Fact]
    public void PrintAnalysisEngines_ShouldPrintOneBannerPerProject()
    {
        var projects = new[]
        {
            new AngularProjectInfo
            {
                Name = "shop", RootPath = "/ws/apps/shop", SourceRoot = "/ws/apps/shop/src",
                ProjectType = AngularProjectType.Application,
                Analysis = new AnalysisReport
                {
                    EngineRequested = AnalysisEngine.Auto, EngineUsed = AnalysisEngineUsed.Ast,
                    TypeScriptVersion = "5.4.5", AngularCompilerVersion = "17.3.0"
                }
            },
            new AngularProjectInfo
            {
                Name = "ui", RootPath = "/ws/libs/ui", SourceRoot = "/ws/libs/ui/src",
                ProjectType = AngularProjectType.Library,
                Analysis = new AnalysisReport
                {
                    EngineRequested = AnalysisEngine.Auto, EngineUsed = AnalysisEngineUsed.Regex,
                    FallbackReason = "Node.js not found — install it"
                }
            }
        };

        var (out_, _) = Capture(() => ResultPrinter.PrintAnalysisEngines(projects));

        out_.Should().Contain("[shop] Analysis engine: AST (typescript 5.4.5");
        out_.Should().Contain("[ui] Analysis engine: regex (Node.js not found — install it)");
    }

    [Fact]
    public void PrintAnalysisEngine_NullReport_ShouldPrintNothing()
    {
        var (out_, err) = Capture(() => ResultPrinter.PrintAnalysisEngine(null));

        out_.Should().BeEmpty();
        err.Should().BeEmpty();
    }
}
