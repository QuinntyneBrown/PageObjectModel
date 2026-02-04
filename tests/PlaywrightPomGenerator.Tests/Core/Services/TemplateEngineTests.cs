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
        result.Should().Contain("import { Page, Locator } from '@playwright/test'");
        result.Should().Contain("export class LoginPage");
        result.Should().Contain("readonly page: Page");
        result.Should().Contain("constructor(page: Page)");
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
        result.Should().Contain("import { test, expect } from '../fixtures'");
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
        result.Should().StartWith("import { Page, Locator }");
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
        result.Should().Contain("import { Page, Locator }");
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
