using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// End-to-end checks that wire the real <see cref="TemplateEngine"/>, <see cref="CodeGenerator"/>,
/// and <see cref="FileSystemService"/> together and verify the component-object TypeScript written
/// to disk. The CLI handler is the only layer not exercised here (it cannot be loaded in the
/// sandbox), but it is a thin wrapper over <see cref="ICodeGenerator.GenerateComponentObjectsAsync"/>.
/// </summary>
public sealed class ComponentObjectIntegrationTests
{
    private readonly ICodeGenerator _generator;
    private readonly GeneratorOptions _options;

    public ComponentObjectIntegrationTests()
    {
        _options = new GeneratorOptions();
        var optionsWrapper = Substitute.For<IOptions<GeneratorOptions>>();
        optionsWrapper.Value.Returns(_options);

        var fileSystem = new FileSystemService();
        var templateEngine = new TemplateEngine(optionsWrapper);
        var analyzer = Substitute.For<IAngularAnalyzer>();

        _generator = new CodeGenerator(
            analyzer,
            templateEngine,
            fileSystem,
            NullLogger<CodeGenerator>.Instance,
            optionsWrapper);
    }

    [Fact]
    public async Task GenerateComponentObjectsAsync_RealServices_ShouldWriteValidRootScopedTypeScript()
    {
        // Arrange
        var outputDir = Path.Combine(Path.GetTempPath(), "PpgComponentObjectsIT_" + Guid.NewGuid().ToString("N"));
        var project = CreateDashboardProject();

        try
        {
            // Act
            var result = await _generator.GenerateComponentObjectsAsync(project, outputDir);

            // Assert
            result.Success.Should().BeTrue();

            var basePath = Path.Combine(outputDir, "component-objects", "base.component.ts");
            var kpiPath = Path.Combine(outputDir, "component-objects", "kpi-card.component.ts");
            var specPath = Path.Combine(outputDir, "tests", "kpi-card.component.spec.ts");

            File.Exists(basePath).Should().BeTrue();
            File.Exists(kpiPath).Should().BeTrue();
            File.Exists(specPath).Should().BeTrue();

            var baseContent = await File.ReadAllTextAsync(basePath);
            baseContent.Should().Contain("export abstract class BaseComponent");
            baseContent.Should().Contain("protected readonly root: Locator");
            baseContent.Should().NotContain("navigate");

            var kpiContent = await File.ReadAllTextAsync(kpiPath);
            kpiContent.Should().Contain("export class KpiCardComponentObject extends BaseComponent");
            kpiContent.Should().Contain("static readonly hostSelector = 'app-kpi-card';");
            kpiContent.Should().Contain("this.kpiTitle = this.root.getByTestId('kpi-title');");
            kpiContent.Should().Contain("this.refreshButton = this.root.getByRole('button', { name: 'Refresh' });");
            kpiContent.Should().NotContain("navigate");
            kpiContent.Should().NotContain("this.page");
            // Braces must balance for valid TypeScript.
            kpiContent.Count(c => c == '{').Should().Be(kpiContent.Count(c => c == '}'));

            var specContent = await File.ReadAllTextAsync(specPath);
            specContent.Should().Contain("page.locator(KpiCardComponentObject.hostSelector).first()");
            specContent.Should().NotContain(".navigate(");
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateComponentObjectsAsync_WithTableAndButton_ShouldRootEverythingOnRootWithoutContamination()
    {
        // Arrange - a component mixing a table with other selectors exercises both code paths at once
        var outputDir = Path.Combine(Path.GetTempPath(), "PpgComponentObjectsIT_" + Guid.NewGuid().ToString("N"));
        var project = new AngularProjectInfo
        {
            Name = "reports",
            RootPath = "/app",
            SourceRoot = "/app/src",
            ProjectType = AngularProjectType.Application,
            Components =
            [
                new AngularComponentInfo
                {
                    Name = "ResultsPanelComponent",
                    Selector = "app-results-panel",
                    FilePath = "/app/src/app/results-panel/results-panel.component.ts",
                    IsRoutable = false,
                    Selectors =
                    [
                        new ElementSelector
                        {
                            ElementType = "mat-table",
                            Strategy = SelectorStrategy.Css,
                            SelectorValue = "mat-table, table[mat-table], [mat-table]",
                            PropertyName = "DataTable",
                            IsTable = true,
                            IsMaterialComponent = true
                        },
                        new ElementSelector
                        {
                            ElementType = "button",
                            Strategy = SelectorStrategy.Role,
                            SelectorValue = "button:has-text(\"Refresh\")",
                            PropertyName = "RefreshButton",
                            TextContent = "Refresh"
                        }
                    ]
                }
            ]
        };

        try
        {
            // Act
            var result = await _generator.GenerateComponentObjectsAsync(project, outputDir);

            // Assert
            result.Success.Should().BeTrue();
            var path = Path.Combine(outputDir, "component-objects", "results-panel.component.ts");
            var content = await File.ReadAllTextAsync(path);

            content.Should().NotContain("this.page");
            content.Should().Contain("this.dataTable = this.root.locator(");
            content.Should().Contain("return this.dataTable.locator('mat-row");
            content.Should().Contain("has: this.root.locator(`:nth-child(");
            content.Should().Contain("async clickRefreshButton(): Promise<void>");
            content.Count(c => c == '{').Should().Be(content.Count(c => c == '}'));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static AngularProjectInfo CreateDashboardProject()
    {
        return new AngularProjectInfo
        {
            Name = "dashboard",
            RootPath = "/app",
            SourceRoot = "/app/src",
            ProjectType = AngularProjectType.Application,
            Components =
            [
                new AngularComponentInfo
                {
                    Name = "KpiCardComponent",
                    Selector = "app-kpi-card",
                    FilePath = "/app/src/app/dashboard/kpi-card/kpi-card.component.ts",
                    IsRoutable = false,
                    Selectors =
                    [
                        new ElementSelector
                        {
                            ElementType = "span",
                            Strategy = SelectorStrategy.TestId,
                            SelectorValue = "[data-testid='kpi-title']",
                            PropertyName = "KpiTitle"
                        },
                        new ElementSelector
                        {
                            ElementType = "span",
                            Strategy = SelectorStrategy.TestId,
                            SelectorValue = "[data-testid='kpi-value']",
                            PropertyName = "KpiValue"
                        },
                        new ElementSelector
                        {
                            ElementType = "button",
                            Strategy = SelectorStrategy.Role,
                            SelectorValue = "button:has-text(\"Refresh\")",
                            PropertyName = "RefreshButton",
                            TextContent = "Refresh"
                        }
                    ]
                }
            ]
        };
    }
}
