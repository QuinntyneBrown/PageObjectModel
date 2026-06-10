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
/// CodeGenerator orchestration for composition: the app flow emits the
/// components/ directory, the TemplateContext carries the generated set and
/// host-page URLs, and the escape hatches restore the 1.x layout.
/// </summary>
public sealed class CodeGeneratorCompositionTests
{
    private readonly IAngularAnalyzer _analyzer = Substitute.For<IAngularAnalyzer>();
    private readonly ITemplateEngine _templateEngine = Substitute.For<ITemplateEngine>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly GeneratorOptions _options = new();

    private CodeGenerator CreateGenerator()
    {
        _templateEngine.GenerateConfig(Arg.Any<AngularProjectInfo>()).Returns("// config");
        _templateEngine.GenerateFixture(Arg.Any<AngularProjectInfo>()).Returns("// fixture");
        _templateEngine.GenerateHelpers().Returns("// helpers");
        _templateEngine.GeneratePageObject(Arg.Any<AngularComponentInfo>(), Arg.Any<TemplateContext?>()).Returns("// page object");
        _templateEngine.GenerateSelectors(Arg.Any<AngularComponentInfo>()).Returns("// selectors");
        _templateEngine.GenerateTestSpec(Arg.Any<AngularComponentInfo>()).Returns("// test spec");
        _templateEngine.GenerateBasePage().Returns("// base page");
        _templateEngine.GenerateBaseComponent().Returns("// base component");
        _templateEngine.GenerateComponentObject(Arg.Any<AngularComponentInfo>(), Arg.Any<TemplateContext?>()).Returns("// component object");
        _templateEngine.GenerateComponentObjectTestSpec(Arg.Any<AngularComponentInfo>(), Arg.Any<TemplateContext?>()).Returns("// component spec");
        _templateEngine.GenerateTimeoutConfig().Returns("// timeouts");
        _templateEngine.GenerateUrlsConfig(Arg.Any<AngularProjectInfo>()).Returns("// urls");

        return new CodeGenerator(
            _analyzer, _templateEngine, _fileSystem,
            Substitute.For<ILogger<CodeGenerator>>(), Options.Create(_options));
    }

    private static AngularProjectInfo ProjectWithComposition() => new()
    {
        Name = "shop",
        RootPath = "/shop",
        SourceRoot = "/shop/src",
        ProjectType = AngularProjectType.Application,
        Components =
        [
            new AngularComponentInfo
            {
                Name = "DashboardComponent",
                Selector = "app-dashboard",
                FilePath = "/shop/src/dashboard.component.ts",
                IsRoutable = true,
                RoutePath = "dashboard",
                ChildComponents =
                [
                    new ChildComponentRef
                    {
                        Selector = "app-kpi-card",
                        ComponentName = "KpiCardComponent",
                        IsRepeated = true
                    }
                ]
            },
            new AngularComponentInfo
            {
                Name = "KpiCardComponent",
                Selector = "app-kpi-card",
                FilePath = "/shop/src/kpi-card.component.ts",
                IsRoutable = false
            }
        ]
    };

    [Fact]
    public async Task GenerateForApplicationAsync_ShouldEmitComponentsDirectoryForCompositionTargets()
    {
        var generator = CreateGenerator();

        var result = await generator.GenerateForApplicationAsync(ProjectWithComposition(), "/output");

        result.Success.Should().BeTrue();
        _fileSystem.CreatedDirectories.Should().Contain(d => d.Contains("components"));
        result.GeneratedFiles.Should().ContainSingle(f => f.RelativePath == "components/base.component.ts");
        result.GeneratedFiles.Should().Contain(f => f.RelativePath == "components/kpi-card.component.ts");
        // The routable page nobody embeds gets no component object.
        result.GeneratedFiles.Should().NotContain(f => f.RelativePath == "components/dashboard.component.ts");
    }

    [Fact]
    public async Task GenerateForApplicationAsync_ShouldPassContextWithGeneratedNamesAndHostUrls()
    {
        var generator = CreateGenerator();
        TemplateContext? captured = null;
        _templateEngine.GeneratePageObject(
                Arg.Any<AngularComponentInfo>(),
                Arg.Do<TemplateContext?>(c => captured ??= c))
            .Returns("// page object");

        await generator.GenerateForApplicationAsync(ProjectWithComposition(), "/output");

        captured.Should().NotBeNull();
        captured!.ComponentObjectNames.Should().Contain("KpiCardComponent");
        captured.HostPageUrls.Should().ContainKey("KpiCardComponent")
            .WhoseValue.Should().Be("/dashboard");
    }

    [Fact]
    public async Task GenerateForApplicationAsync_WithEmitComponentObjectsOff_ShouldRestoreLegacyLayout()
    {
        _options.EmitComponentObjects = false;
        var generator = CreateGenerator();

        var result = await generator.GenerateForApplicationAsync(ProjectWithComposition(), "/output");

        result.GeneratedFiles.Should().NotContain(f => f.RelativePath.StartsWith("components/"));
        result.Warnings.Should().Contain(w => w.Contains("Composition accessors skipped") && w.Contains("KpiCardComponent"));
    }

    [Fact]
    public async Task GenerateForApplicationAsync_WithEmitCompositionOff_ShouldPassEmptyNameSet()
    {
        _options.EmitComposition = false;
        var generator = CreateGenerator();
        TemplateContext? captured = null;
        _templateEngine.GeneratePageObject(
                Arg.Any<AngularComponentInfo>(),
                Arg.Do<TemplateContext?>(c => captured ??= c))
            .Returns("// page object");

        var result = await generator.GenerateForApplicationAsync(ProjectWithComposition(), "/output");

        // Component objects still exist; pages just don't embed them.
        result.GeneratedFiles.Should().Contain(f => f.RelativePath == "components/kpi-card.component.ts");
        captured!.ComponentObjectNames.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateComponentObjectsAsync_ShouldPassContextForComponentToComponentComposition()
    {
        var generator = CreateGenerator();
        TemplateContext? captured = null;
        _templateEngine.GenerateComponentObject(
                Arg.Any<AngularComponentInfo>(),
                Arg.Do<TemplateContext?>(c => captured ??= c))
            .Returns("// component object");

        await generator.GenerateComponentObjectsAsync(ProjectWithComposition(), "/output");

        captured.Should().NotBeNull();
        captured!.ComponentObjectNames.Should().Contain(["DashboardComponent", "KpiCardComponent"]);
        captured.HostPageUrls.Should().ContainKey("KpiCardComponent");
    }
}
