using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;
using PlaywrightPomGenerator.Tests.TestUtilities;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// Engine selection and AST-to-model mapping in <see cref="AngularAnalyzer"/>:
/// Auto falls back to regex, Ast rethrows, Regex never probes the sidecar, and
/// successful AST results replace regex discovery (with per-component template
/// fallback).
/// </summary>
public sealed class AngularAnalyzerEngineTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IAstProjectAnalyzer _astAnalyzer = Substitute.For<IAstProjectAnalyzer>();
    private readonly IPackageInspector _packageInspector = Substitute.For<IPackageInspector>();

    public AngularAnalyzerEngineTests()
    {
        _packageInspector.InspectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PackageReport.Empty);
    }

    private AngularAnalyzer CreateAnalyzer(AnalysisEngine engine) => new(
        _fileSystem,
        Substitute.For<ILogger<AngularAnalyzer>>(),
        _astAnalyzer,
        _packageInspector,
        Options.Create(new GeneratorOptions { AnalysisEngine = engine }));

    private void AddRegexDiscoverableApp()
    {
        _fileSystem.AddDirectory("/app/src");
        _fileSystem.AddFile("/app/src/login.component.ts", """
            import { Component } from '@angular/core';
            @Component({
              selector: 'app-login',
              template: '<button data-testid="login-button">Login</button>'
            })
            export class LoginComponent {}
            """);
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_AutoWithSidecarUnavailable_ShouldFallBackToRegex()
    {
        // Arrange
        AddRegexDiscoverableApp();
        _astAnalyzer.AnalyzeProjectAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AstProjectTarget>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task<AstProjectAnalysis>>(_ => throw new SidecarUnavailableException(
                SidecarUnavailableReason.NodeMissing, "no node"));

        // Act
        var project = await CreateAnalyzer(AnalysisEngine.Auto).AnalyzeApplicationAsync("/app");

        // Assert — regex still finds the component; the report explains the fallback.
        project.Components.Should().ContainSingle(c => c.Name == "LoginComponent");
        project.Analysis.Should().NotBeNull();
        project.Analysis!.EngineUsed.Should().Be(AnalysisEngineUsed.Regex);
        project.Analysis.EngineRequested.Should().Be(AnalysisEngine.Auto);
        project.Analysis.FallbackReason.Should().Contain("Node.js");
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_RegexEngine_ShouldNeverInvokeTheSidecar()
    {
        // Arrange
        AddRegexDiscoverableApp();

        // Act
        var project = await CreateAnalyzer(AnalysisEngine.Regex).AnalyzeApplicationAsync("/app");

        // Assert
        project.Analysis!.EngineUsed.Should().Be(AnalysisEngineUsed.Regex);
        project.Analysis.FallbackReason.Should().Be("--engine regex");
        await _astAnalyzer.DidNotReceiveWithAnyArgs()
            .AnalyzeProjectAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_AstEngineWithFailure_ShouldThrow()
    {
        // Arrange
        AddRegexDiscoverableApp();
        _astAnalyzer.AnalyzeProjectAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AstProjectTarget>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task<AstProjectAnalysis>>(_ => throw new SidecarUnavailableException(
                SidecarUnavailableReason.TypeScriptMissing, "no typescript"));

        // Act
        var act = () => CreateAnalyzer(AnalysisEngine.Ast).AnalyzeApplicationAsync("/app");

        // Assert — explicit opt-in must not silently degrade.
        await act.Should().ThrowAsync<SidecarUnavailableException>();
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_AstSuccess_ShouldMapComponentsRoutesAndEnrichment()
    {
        // Arrange
        _fileSystem.AddDirectory("/app/src");
        var analysis = new AstProjectAnalysis
        {
            SchemaVersion = 1,
            Engine = new AstEngineInfo { TypeScript = "5.4.5", AngularCompiler = "17.3.0" },
            Projects =
            [
                new AstProjectResult
                {
                    Name = "app",
                    Components =
                    [
                        new AstComponent
                        {
                            ClassName = "OrderDetailComponent",
                            FilePath = "/app/src/order-detail.component.ts",
                            Selector = "app-order-detail",
                            Selectors = ["app-order-detail"],
                            Standalone = true,
                            TemplateSource = "external",
                            TemplateUrl = "/app/src/order-detail.component.html",
                            TemplateParsed = true,
                            Inputs = [new AstPort { Name = "orderId", Kind = "signal", Required = true }],
                            Template = new AstTemplate
                            {
                                Elements =
                                [
                                    new AstElement
                                    {
                                        Tag = "button",
                                        TestId = "refresh",
                                        Text = new AstText { Value = "Refresh" },
                                        Events = ["click"],
                                        Handlers = new Dictionary<string, string> { ["click"] = "refresh" }
                                    }
                                ],
                                Forms =
                                [
                                    new AstForm
                                    {
                                        FormGroup = "orderForm",
                                        SubmitHandler = "save",
                                        Controls = [new AstFormControl { Name = "note", Tag = "textarea" }]
                                    }
                                ]
                            },
                            ChildComponents =
                            [
                                new AstChildComponent
                                {
                                    Selector = "app-line-item",
                                    ComponentClassName = "LineItemComponent",
                                    ComponentFilePath = "/app/src/line-item.component.ts",
                                    Count = 2,
                                    Repeated = true
                                }
                            ]
                        }
                    ],
                    Routes = new AstRouteAnalysis
                    {
                        Tree =
                        [
                            new AstRouteNode
                            {
                                Path = "orders/:orderId",
                                FullPath = "/orders/:orderId",
                                Component = "OrderDetailComponent",
                                ComponentFilePath = "/app/src/order-detail.component.ts",
                                PathParams = ["orderId"],
                                Title = "Order",
                                IsRoot = true
                            }
                        ],
                        ComponentRoutes =
                        [
                            new AstComponentRoute
                            {
                                ComponentFilePath = "/app/src/order-detail.component.ts",
                                ComponentClassName = "OrderDetailComponent",
                                FullPaths = ["/orders/:orderId"],
                                Titles = ["Order"]
                            }
                        ]
                    }
                }
            ]
        };
        _astAnalyzer.AnalyzeProjectAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AstProjectTarget>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(analysis);

        // Act
        var project = await CreateAnalyzer(AnalysisEngine.Auto).AnalyzeApplicationAsync("/app");

        // Assert
        project.Analysis!.EngineUsed.Should().Be(AnalysisEngineUsed.Ast);
        project.Analysis.AngularCompilerVersion.Should().Be("17.3.0");

        var component = project.Components.Should().ContainSingle().Subject;
        component.Name.Should().Be("OrderDetailComponent");
        component.IsStandalone.Should().BeTrue();
        component.Selectors.Should().ContainSingle(s => s.TestIdValue == "refresh");
        component.InputsDetailed.Should().ContainSingle(p => p.Name == "orderId" && p.Kind == ComponentPortKind.Signal);

        // Route linkage: the long-missing RoutePath/RouteParams/RouteEvidence.
        component.RoutePath.Should().Be("orders/:orderId");
        component.RouteParams.Should().BeEquivalentTo(["orderId"]);
        component.RouteEvidence.Should().BeTrue();
        component.IsRoutable.Should().BeTrue("route evidence overrides the name heuristic");
        component.TitleFromRoute.Should().Be("Order");

        component.ChildComponents.Should().ContainSingle(c => c.ComponentName == "LineItemComponent" && c.IsRepeated);
        var form = component.Forms.Should().ContainSingle().Subject;
        form.FormGroupName.Should().Be("orderForm");
        form.Controls.Should().ContainSingle(c => c.ControlName == "note" && c.ControlType == ControlType.Textarea);

        var route = project.Routes.Should().ContainSingle().Subject;
        route.FullPath.Should().Be("/orders/:orderId");
        route.PathParameters.Should().BeEquivalentTo(["orderId"]);
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_ComponentWithUnparsedTemplate_ShouldFallBackToRegexForThatComponent()
    {
        // Arrange — the AST result flags the template as unparseable, but the
        // template file exists on disk for the regex fallback.
        _fileSystem.AddDirectory("/app/src");
        _fileSystem.AddFile("/app/src/legacy.component.html",
            "<button data-testid=\"legacy-action\">Act</button>");
        var analysis = new AstProjectAnalysis
        {
            SchemaVersion = 1,
            Engine = new AstEngineInfo { TypeScript = "5.4.5", AngularCompiler = "17.3.0" },
            Projects =
            [
                new AstProjectResult
                {
                    Name = "app",
                    Components =
                    [
                        new AstComponent
                        {
                            ClassName = "LegacyComponent",
                            FilePath = "/app/src/legacy.component.ts",
                            Selector = "app-legacy",
                            TemplateSource = "external",
                            TemplateUrl = "/app/src/legacy.component.html",
                            TemplateParsed = false,
                            TemplateErrors = ["parse failed"]
                        }
                    ]
                }
            ]
        };
        _astAnalyzer.AnalyzeProjectAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AstProjectTarget>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(analysis);

        // Act
        var project = await CreateAnalyzer(AnalysisEngine.Auto).AnalyzeApplicationAsync("/app");

        // Assert — regex selectors for that component, with a recorded warning.
        var component = project.Components.Should().ContainSingle().Subject;
        component.Selectors.Should().ContainSingle(s =>
            s.Strategy == SelectorStrategy.TestId && s.SelectorValue == "[data-testid='legacy-action']");
        project.Analysis!.Warnings.Should().Contain(w => w.Contains("LegacyComponent") && w.Contains("regex fallback"));
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_WithoutCompiler_ShouldReportMixedMode()
    {
        // Arrange
        _fileSystem.AddDirectory("/app/src");
        var analysis = new AstProjectAnalysis
        {
            SchemaVersion = 1,
            Engine = new AstEngineInfo { TypeScript = "5.4.5", AngularCompiler = null },
            Projects = [new AstProjectResult { Name = "app" }]
        };
        _astAnalyzer.AnalyzeProjectAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AstProjectTarget>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(analysis);

        // Act
        var project = await CreateAnalyzer(AnalysisEngine.Auto).AnalyzeApplicationAsync("/app");

        // Assert
        project.Analysis!.EngineUsed.Should().Be(AnalysisEngineUsed.AstWithRegexTemplates);
    }

    [Fact]
    public async Task AnalyzeWorkspaceAsync_AstSuccess_ShouldAnalyzeAllProjectsInOneSidecarCall()
    {
        // Arrange
        _fileSystem.AddFile("/ws/angular.json", """
            {
              "version": 1,
              "projects": {
                "shop": { "projectType": "application", "root": "apps/shop", "sourceRoot": "apps/shop/src", "prefix": "shop" },
                "ui": { "projectType": "library", "root": "libs/ui", "sourceRoot": "libs/ui/src", "prefix": "ui" }
              }
            }
            """);
        var analysis = new AstProjectAnalysis
        {
            SchemaVersion = 1,
            Engine = new AstEngineInfo { TypeScript = "5.4.5", AngularCompiler = "17.3.0" },
            Projects =
            [
                new AstProjectResult { Name = "shop" },
                new AstProjectResult { Name = "ui" }
            ]
        };
        IReadOnlyList<AstProjectTarget>? capturedTargets = null;
        _astAnalyzer.AnalyzeProjectAsync(
                Arg.Any<string>(),
                Arg.Do<IReadOnlyList<AstProjectTarget>>(t => capturedTargets = t),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(analysis);

        // Act
        var workspace = await CreateAnalyzer(AnalysisEngine.Auto).AnalyzeWorkspaceAsync("/ws");

        // Assert — one batched call covering both projects, prefixes included.
        workspace.Projects.Should().HaveCount(2);
        await _astAnalyzer.Received(1).AnalyzeProjectAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<AstProjectTarget>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        capturedTargets.Should().NotBeNull();
        capturedTargets!.Select(t => (t.Name, t.Prefix)).Should().BeEquivalentTo([("shop", "shop"), ("ui", "ui")]);
        workspace.Projects.Should().OnlyContain(p => p.Analysis!.EngineUsed == AnalysisEngineUsed.Ast);
    }
}
