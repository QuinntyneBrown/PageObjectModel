using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class TemplateEngineTests
{
    private readonly TemplateEngine _engine;
    private readonly GeneratorOptions _options;

    public TemplateEngineTests()
    {
        _options = new GeneratorOptions();
        var optionsWrapper = Substitute.For<IOptions<GeneratorOptions>>();
        optionsWrapper.Value.Returns(_options);
        _engine = new TemplateEngine(optionsWrapper);
    }

    [Fact]
    public void GenerateFileHeader_WithEmptyDefault_ShouldReturnEmptyString()
    {
        // Act (default FileHeader is empty)
        var result = _engine.GenerateFileHeader("test-file.ts");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateFileHeader_WithCustomHeader_ShouldReplacePlaceholders()
    {
        // Arrange
        _options.FileHeader = """
            /**
             * @file {FileName}
             * @version {ToolVersion}
             * @date {GeneratedDate}
             */
            """;

        // Act
        var result = _engine.GenerateFileHeader("test-file.ts");

        // Assert
        result.Should().Contain("test-file.ts");
        result.Should().Contain(_options.ToolVersion);
        result.Should().NotContain("{FileName}");
        result.Should().NotContain("{GeneratedDate}");
        result.Should().NotContain("{ToolVersion}");
    }

    [Fact]
    public void GeneratePageObject_ShouldGenerateValidTypeScript()
    {
        // Arrange
        var component = CreateTestComponent();

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert
        result.Should().Contain("import { Page, Locator, expect } from '@playwright/test'");
        result.Should().Contain("import { BasePage } from './base.page'");
        result.Should().Contain("export class Login extends BasePage");
        result.Should().Contain("constructor(page: Page)");
        result.Should().Contain("super(page)");
    }

    [Fact]
    public void GeneratePageObject_WithSelectors_ShouldGenerateLocators()
    {
        // Arrange
        var component = CreateTestComponent();

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert
        result.Should().Contain("readonly submitButton: Locator");
        result.Should().Contain("readonly usernameInput: Locator");
    }

    [Fact]
    public void GeneratePageObject_WithJsDocEnabled_ShouldIncludeJsDocComments()
    {
        // Arrange
        var component = CreateTestComponent();
        _options.GenerateJsDocComments = true;

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert
        result.Should().Contain("/**");
        result.Should().Contain("*/");
        result.Should().Contain("* @param");
    }

    [Fact]
    public void GeneratePageObject_WithJsDocDisabled_ShouldNotIncludeJsDocComments()
    {
        // Arrange
        var component = CreateTestComponent();
        _options.GenerateJsDocComments = false;

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert
        // The file header may contain /** but method-level JSDoc (@param, @returns) should not be present
        result.Should().NotContain("@param");
        result.Should().NotContain("@returns");
        result.Should().NotContain("* Page Object for the");
        result.Should().NotContain("* Locator for the");
    }

    [Fact]
    public void GenerateSelectors_ShouldGenerateSelectorsObject()
    {
        // Arrange
        var component = CreateTestComponent();

        // Act
        var result = _engine.GenerateSelectors(component);

        // Assert
        result.Should().Contain("export const loginComponentSelectors");
        result.Should().Contain("as const");
    }

    [Fact]
    public void GenerateFixture_ShouldGenerateTestExtension()
    {
        // Arrange
        var project = CreateTestProject();

        // Act
        var result = _engine.GenerateFixture(project);

        // Assert
        result.Should().Contain("import { test as base } from '@playwright/test'");
        result.Should().Contain("export const test = base.extend");
        result.Should().Contain("export { expect } from '@playwright/test'");
    }

    [Fact]
    public void GenerateConfig_ShouldGeneratePlaywrightConfig()
    {
        // Arrange
        var project = CreateTestProject();

        // Act
        var result = _engine.GenerateConfig(project);

        // Assert
        result.Should().Contain("import { defineConfig, devices } from '@playwright/test'");
        result.Should().Contain("export default defineConfig");
        result.Should().Contain("testDir: './tests'");
        result.Should().Contain(_options.BaseUrlPlaceholder);
        result.Should().Contain($"actionTimeout: {_options.DefaultTimeout}");
    }

    [Fact]
    public void GenerateHelpers_ShouldGenerateUtilityFunctions()
    {
        // Act
        var result = _engine.GenerateHelpers();

        // Assert
        result.Should().Contain("export async function waitForStable");
        result.Should().Contain("export async function fillAndVerify");
        result.Should().Contain("export async function clickAndWaitForNavigation");
        result.Should().Contain("export async function retry");
        result.Should().Contain("export async function takeScreenshot");
    }

    [Fact]
    public void GenerateTestSpec_ShouldGenerateTestFile()
    {
        // Arrange
        var component = CreateTestComponent();

        // Act
        var result = _engine.GenerateTestSpec(component);

        // Assert
        result.Should().Contain("import { test, expect } from '../fixtures/fixtures'");
        result.Should().Contain("test.describe('LoginComponent'");
        result.Should().Contain("should display the component");
    }

    [Fact]
    public void GenerateTestSpec_WithCustomTestSuffix_ShouldUseConfiguredSuffix()
    {
        // Arrange
        var component = CreateTestComponent();
        _options.TestFileSuffix = "test";
        _options.FileHeader = "// Header: {FileName}";

        // Act
        var result = _engine.GenerateTestSpec(component);

        // Assert - the header should use the new suffix in the filename
        result.Should().Contain("// Header: login.test.ts");
    }

    [Fact]
    public void GeneratePageObject_WithEmptyHeader_ShouldNotIncludeHeader()
    {
        // Arrange
        var component = CreateTestComponent();
        _options.GenerateJsDocComments = false;

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert - should start directly with import
        result.Should().StartWith("import { Page, Locator, expect }");
    }

    [Fact]
    public void GeneratePageObject_WithCustomHeader_ShouldIncludeHeader()
    {
        // Arrange
        var component = CreateTestComponent();
        _options.FileHeader = "// Custom header for {FileName}";

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert
        result.Should().StartWith("// Custom header for login.page.ts");
        result.Should().Contain("import { Page, Locator, expect }");
        result.Should().Contain("import { BasePage }");
    }

    [Fact]
    public void GenerateSignalRMock_ShouldGenerateRxJSBasedMock()
    {
        // Act
        var result = _engine.GenerateSignalRMock();

        // Assert
        result.Should().Contain("import { Subject, Observable, BehaviorSubject, ReplaySubject } from 'rxjs'");
        result.Should().Contain("export class MockHubConnection");
        result.Should().Contain("HubConnectionState");
        result.Should().Contain("start(): Observable<void>");
        result.Should().Contain("stop(): Observable<void>");
        result.Should().Contain("stream<T>(methodName: string): Observable<T>");
        result.Should().Contain("invoke<T>(methodName: string");
        result.Should().Contain("simulateServerMessage");
        result.Should().Contain("simulateError");
        result.Should().Contain("simulateReconnect");
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TemplateEngine(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateFileHeader_WithNullFileName_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _engine.GenerateFileHeader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GeneratePageObject_WithNullComponent_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _engine.GeneratePageObject(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ---------------------------------------------------------------------
    // Refactor regression guards: page objects must still root on `page`.
    // ---------------------------------------------------------------------

    [Fact]
    public void GeneratePageObject_ShouldStillRootLocatorsOnPage()
    {
        // Arrange
        var component = CreateTestComponent();

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert - page objects remain page-rooted (the refactor must not leak this.root)
        result.Should().Contain("page.getByTestId('submit')");
        result.Should().NotContain("this.root");
        result.Should().Contain("import { Page, Locator, expect }");
    }

    [Fact]
    public void GeneratePageObject_WithTable_ShouldRootColumnFilterOnThisPage()
    {
        // Arrange
        var component = CreateTableComponent();

        // Act
        var result = _engine.GeneratePageObject(component);

        // Assert - the table column accessor stays rooted on this.page for page objects,
        // and the row accessor remains scoped to the table field (no this.root leakage)
        result.Should().Contain("has: this.page.locator(`:nth-child(");
        result.Should().Contain("return this.dataTable.locator('mat-row");
        result.Should().NotContain("this.root");
    }

    // ---------------------------------------------------------------------
    // Base component
    // ---------------------------------------------------------------------

    [Fact]
    public void GenerateBaseComponent_ShouldGenerateRootScopedAbstractClass()
    {
        // Act
        var result = _engine.GenerateBaseComponent();

        // Assert
        result.Should().Contain("import { Locator, expect } from '@playwright/test'");
        result.Should().Contain("export abstract class BaseComponent");
        result.Should().Contain("protected readonly root: Locator");
        result.Should().Contain("constructor(root: Locator)");
        result.Should().Contain("this.root = root;");
        result.Should().Contain("getRoot(): Locator");
        result.Should().Contain("async expectVisible(): Promise<void>");
        result.Should().Contain("await expect(this.root).toBeVisible();");
        result.Should().Contain("async expectHidden(): Promise<void>");
        result.Should().Contain("await expect(this.root).toBeHidden();");
        result.Should().Contain("async isVisible(): Promise<boolean>");
        result.Should().Contain("protected getByTestId(testId: string): Locator");
    }

    [Fact]
    public void GenerateBaseComponent_ShouldNotDeclareNavigateOrDependOnPage()
    {
        // Act
        var result = _engine.GenerateBaseComponent();

        // Assert - component objects compose; they do not navigate and do not depend on the Page type
        result.Should().NotContain("navigate");
        result.Should().NotContain("goto");
        result.Should().NotContain("import { Page");
        result.Should().NotContain(": Page");
    }

    // ---------------------------------------------------------------------
    // Component objects
    // ---------------------------------------------------------------------

    [Fact]
    public void GenerateComponentObject_ShouldGenerateRootScopedClass()
    {
        // Arrange
        var component = CreateKpiCardComponent();

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert
        result.Should().Contain("import { Locator, expect } from '@playwright/test'");
        result.Should().Contain("import { BaseComponent } from './base.component'");
        result.Should().Contain("export class KpiCard extends BaseComponent");
        result.Should().Contain("static readonly hostSelector = 'app-kpi-card';");
        result.Should().Contain("constructor(root: Locator)");
        result.Should().Contain("super(root);");
    }

    [Fact]
    public void GenerateComponentObject_ShouldRootEveryLocatorOnThisRoot()
    {
        // Arrange
        var component = CreateKpiCardComponent();

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert - locators are scoped under this.root, never page
        result.Should().Contain("this.kpiTitle = this.root.getByTestId('kpi-title');");
        result.Should().Contain("this.kpiValue = this.root.getByTestId('kpi-value');");
        result.Should().Contain("this.refreshButton = this.root.getByRole('button', { name: 'Refresh' });");
        result.Should().NotContain("page.getByTestId");
        result.Should().NotContain("this.page");
    }

    [Fact]
    public void GenerateComponentObject_ShouldNotNavigateOrExposeComponentSelector()
    {
        // Arrange
        var component = CreateKpiCardComponent();

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert
        result.Should().NotContain("navigate");
        result.Should().NotContain("goto");
        result.Should().NotContain("componentSelector");
        result.Should().NotContain("import { Page");
    }

    [Fact]
    public void GenerateComponentObject_ShouldEmitAssertionAndActionHelpersRootedOnRoot()
    {
        // Arrange
        var component = CreateKpiCardComponent();

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert
        result.Should().Contain("async clickRefreshButton(): Promise<void>");
        result.Should().Contain("await this.refreshButton.click();");
        result.Should().Contain("async expectRefreshButtonVisible(): Promise<void>");
        result.Should().Contain("await expect(this.refreshButton).toBeVisible();");
    }

    [Fact]
    public void GenerateComponentObject_WithTable_ShouldRootColumnFilterOnThisRoot()
    {
        // Arrange
        var component = CreateTableComponent();

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert - tables inside components are scoped to this.root, not page
        result.Should().Contain("has: this.root.locator(`:nth-child(");
        result.Should().NotContain("this.page");
    }

    [Fact]
    public void GenerateComponentObject_WithNoSelectors_ShouldStillEmitHostSelectorAndExtendBase()
    {
        // Arrange
        var component = new AngularComponentInfo
        {
            Name = "EmptyWidgetComponent",
            Selector = "app-empty-widget",
            FilePath = "/src/app/empty-widget/empty-widget.component.ts"
        };

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert
        result.Should().Contain("export class EmptyWidget extends BaseComponent");
        result.Should().Contain("static readonly hostSelector = 'app-empty-widget';");
        result.Should().Contain("constructor(root: Locator)");
        result.Should().Contain("super(root);");
        result.Should().NotContain("navigate");
    }

    [Fact]
    public void GenerateComponentObject_WithoutComponentSuffix_ShouldUseNameAsIs()
    {
        // Arrange
        var component = new AngularComponentInfo
        {
            Name = "Widget",
            Selector = "app-widget",
            FilePath = "/src/app/widget/widget.ts"
        };

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert
        result.Should().Contain("export class Widget extends BaseComponent");
    }

    [Fact]
    public void GenerateComponentObject_WithJsDocDisabled_ShouldOmitMethodJsDoc()
    {
        // Arrange
        var component = CreateKpiCardComponent();
        _options.GenerateJsDocComments = false;

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert
        result.Should().NotContain("@param");
        result.Should().NotContain("@returns");
        result.Should().NotContain("* Component Object for the");
        result.Should().StartWith("import { Locator, expect }");
    }

    [Fact]
    public void GenerateComponentObject_WithNullComponent_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _engine.GenerateComponentObject(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ---------------------------------------------------------------------
    // Component-object specs
    // ---------------------------------------------------------------------

    [Fact]
    public void GenerateComponentObjectTestSpec_ShouldComposeFromHostPageWithoutNavigate()
    {
        // Arrange
        var component = CreateKpiCardComponent();

        // Act
        var result = _engine.GenerateComponentObjectTestSpec(component);

        // Assert
        result.Should().Contain("import { test, expect } from '@playwright/test'");
        result.Should().Contain("import { KpiCard } from '../components/kpi-card.component'");
        result.Should().Contain("const HOST_PAGE_URL");
        result.Should().Contain("await page.goto(HOST_PAGE_URL);");
        result.Should().Contain("new KpiCard(");
        result.Should().Contain("page.locator(KpiCard.hostSelector).first()");
        result.Should().Contain("await component.expectVisible();");
        result.Should().NotContain(".navigate(");
        result.Should().NotContain("fixtures");
    }

    [Fact]
    public void GenerateComponentObjectTestSpec_ShouldScaffoldGeneratedAssertions()
    {
        // Arrange
        var component = CreateKpiCardComponent();

        // Act
        var result = _engine.GenerateComponentObjectTestSpec(component);

        // Assert - at least one generated expect* helper is exercised
        result.Should().Contain("await component.expectRefreshButtonVisible();");
    }

    [Fact]
    public void GenerateComponentObjectTestSpec_WithManyAssertableElements_ShouldScaffoldAtMostTwoAssertions()
    {
        // Arrange - three buttons all produce expect*Visible helpers
        var component = new AngularComponentInfo
        {
            Name = "ToolbarComponent",
            Selector = "app-toolbar",
            FilePath = "/src/app/toolbar/toolbar.component.ts",
            Selectors =
            [
                ButtonSelector("SaveButton", "Save"),
                ButtonSelector("CancelButton", "Cancel"),
                ButtonSelector("DeleteButton", "Delete")
            ]
        };

        // Act
        var result = _engine.GenerateComponentObjectTestSpec(component);

        // Assert - the scaffold caps generated assertions at two (.Take(2))
        System.Text.RegularExpressions.Regex.Matches(result, "should be visible'").Count.Should().Be(2);
        result.Should().Contain("await component.expectSaveButtonVisible();");
        result.Should().Contain("await component.expectCancelButtonVisible();");
        result.Should().NotContain("await component.expectDeleteButtonVisible();");
    }

    [Fact]
    public void GenerateComponentObject_WithTable_ShouldRootAllTableAccessorsOnThisRoot()
    {
        // Arrange
        var component = CreateTableComponent();

        // Act
        var result = _engine.GenerateComponentObject(component);

        // Assert - every table accessor chains off the this.root-rooted field, never page
        result.Should().Contain("return this.dataTable.locator('mat-row");
        result.Should().Contain("return this.dataTable.locator('mat-header-cell");
        result.Should().Contain("has: this.root.locator(`:nth-child(");
        result.Should().NotContain("this.page");
    }

    [Fact]
    public void GenerateComponentObjectTestSpec_WithCustomSuffix_ShouldUseSuffixInHeaderFileName()
    {
        // Arrange
        var component = CreateKpiCardComponent();
        _options.TestFileSuffix = "test";
        _options.FileHeader = "// Header: {FileName}";

        // Act
        var result = _engine.GenerateComponentObjectTestSpec(component);

        // Assert
        result.Should().Contain("// Header: kpi-card.component.test.ts");
    }

    [Fact]
    public void GenerateComponentObjectTestSpec_WithNullComponent_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _engine.GenerateComponentObjectTestSpec(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static AngularComponentInfo CreateKpiCardComponent()
    {
        return new AngularComponentInfo
        {
            Name = "KpiCardComponent",
            Selector = "app-kpi-card",
            FilePath = "/src/app/dashboard/kpi-card/kpi-card.component.ts",
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
        };
    }

    private static ElementSelector ButtonSelector(string propertyName, string text) => new()
    {
        ElementType = "button",
        Strategy = SelectorStrategy.Role,
        SelectorValue = $"button:has-text(\"{text}\")",
        PropertyName = propertyName,
        TextContent = text
    };

    private static AngularComponentInfo CreateTableComponent()
    {
        return new AngularComponentInfo
        {
            Name = "ResultsTableComponent",
            Selector = "app-results-table",
            FilePath = "/src/app/results-table/results-table.component.ts",
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
                }
            ]
        };
    }

    private static AngularComponentInfo CreateTestComponent()
    {
        return new AngularComponentInfo
        {
            Name = "LoginComponent",
            Selector = "app-login",
            FilePath = "/src/app/login/login.component.ts",
            Selectors =
            [
                new ElementSelector
                {
                    ElementType = "button",
                    Strategy = SelectorStrategy.TestId,
                    SelectorValue = "[data-testid='submit']",
                    PropertyName = "SubmitButton",
                    TextContent = "Submit"
                },
                new ElementSelector
                {
                    ElementType = "input",
                    Strategy = SelectorStrategy.Css,
                    SelectorValue = "[formControlName='username']",
                    PropertyName = "UsernameInput"
                }
            ]
        };
    }

    private static AngularProjectInfo CreateTestProject()
    {
        return new AngularProjectInfo
        {
            Name = "test-app",
            RootPath = "/app",
            SourceRoot = "/app/src",
            ProjectType = AngularProjectType.Application,
            Components = [CreateTestComponent()]
        };
    }
}
