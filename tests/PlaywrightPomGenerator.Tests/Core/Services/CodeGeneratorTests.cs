using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;
using PlaywrightPomGenerator.Tests.TestUtilities;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class CodeGeneratorTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IAngularAnalyzer _analyzer;
    private readonly ITemplateEngine _templateEngine;
    private readonly ILogger<CodeGenerator> _logger;
    private readonly GeneratorOptions _options;
    private readonly CodeGenerator _generator;

    public CodeGeneratorTests()
    {
        _fileSystem = new MockFileSystem();
        _analyzer = Substitute.For<IAngularAnalyzer>();
        _templateEngine = Substitute.For<ITemplateEngine>();
        _logger = Substitute.For<ILogger<CodeGenerator>>();
        _options = new GeneratorOptions();

        var optionsWrapper = Substitute.For<IOptions<GeneratorOptions>>();
        optionsWrapper.Value.Returns(_options);

        // Setup template engine defaults
        _templateEngine.GenerateConfig(Arg.Any<AngularProjectInfo>()).Returns("// config");
        _templateEngine.GenerateFixture(Arg.Any<AngularProjectInfo>()).Returns("// fixture");
        _templateEngine.GenerateHelpers().Returns("// helpers");
        _templateEngine.GeneratePageObject(Arg.Any<AngularComponentInfo>()).Returns("// page object");
        _templateEngine.GenerateSelectors(Arg.Any<AngularComponentInfo>()).Returns("// selectors");
        _templateEngine.GenerateTestSpec(Arg.Any<AngularComponentInfo>()).Returns("// test spec");
        _templateEngine.GenerateSignalRMock().Returns("// signalr mock");

        _generator = new CodeGenerator(_analyzer, _templateEngine, _fileSystem, _logger, optionsWrapper);
    }

    [Fact]
    public async Task GenerateForApplicationAsync_ShouldGenerateAllFiles()
    {
        // Arrange
        var project = CreateTestProject();

        // Act
        var result = await _generator.GenerateForApplicationAsync(project, "/output");

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().NotBeEmpty();
        result.GeneratedFiles.Should().Contain(f => f.FileType == GeneratedFileType.Config);
        result.GeneratedFiles.Should().Contain(f => f.FileType == GeneratedFileType.Fixture);
        result.GeneratedFiles.Should().Contain(f => f.FileType == GeneratedFileType.Helper);
    }

    [Fact]
    public async Task GenerateForApplicationAsync_WithComponents_ShouldGenerateComponentFiles()
    {
        // Arrange
        var project = CreateTestProject();

        // Act
        var result = await _generator.GenerateForApplicationAsync(project, "/output");

        // Assert
        result.GeneratedFiles.Should().Contain(f => f.FileType == GeneratedFileType.PageObject);
        result.GeneratedFiles.Should().Contain(f => f.FileType == GeneratedFileType.Selectors);
        result.GeneratedFiles.Should().Contain(f => f.FileType == GeneratedFileType.TestSpec);
    }

    [Fact]
    public async Task GenerateForApplicationAsync_ShouldCreateOutputDirectories()
    {
        // Arrange
        var project = CreateTestProject();

        // Act
        await _generator.GenerateForApplicationAsync(project, "/output");

        // Assert
        _fileSystem.CreatedDirectories.Should().Contain(d => d.Contains("pages"));
        _fileSystem.CreatedDirectories.Should().Contain(d => d.Contains("selectors"));
        _fileSystem.CreatedDirectories.Should().Contain(d => d.Contains("tests"));
    }

    [Fact]
    public async Task GenerateForWorkspaceAsync_WithMultipleProjects_ShouldGenerateForAll()
    {
        // Arrange
        var workspace = CreateTestWorkspace();

        // Act
        var result = await _generator.GenerateForWorkspaceAsync(workspace, "/output");

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateForWorkspaceAsync_WithSpecificProject_ShouldGenerateOnlyForThat()
    {
        // Arrange
        var workspace = CreateTestWorkspace();

        // Act
        var result = await _generator.GenerateForWorkspaceAsync(workspace, "/output", "app1");

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateForWorkspaceAsync_WithNonExistentProject_ShouldReturnError()
    {
        // Arrange
        var workspace = CreateTestWorkspace();

        // Act
        var result = await _generator.GenerateForWorkspaceAsync(workspace, "/output", "non-existent");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task GenerateArtifactsAsync_WithNoOptions_ShouldReturnError()
    {
        // Arrange
        var request = new GenerationRequest
        {
            TargetPath = "/app"
        };

        // Act
        var result = await _generator.GenerateArtifactsAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("No generation options specified");
    }

    [Fact]
    public async Task GenerateArtifactsAsync_WithFixturesOnly_ShouldGenerateOnlyFixtures()
    {
        // Arrange
        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(true);
        _analyzer.AnalyzeWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestWorkspace());

        var request = new GenerationRequest
        {
            TargetPath = "/app",
            GenerateFixtures = true
        };

        // Act
        var result = await _generator.GenerateArtifactsAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().Contain(f => f.FileType == GeneratedFileType.Fixture);
    }

    [Fact]
    public async Task GenerateSignalRMockAsync_ShouldGenerateMockFile()
    {
        // Act
        var result = await _generator.GenerateSignalRMockAsync("/output");

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].FileType.Should().Be(GeneratedFileType.SignalRMock);
        result.GeneratedFiles[0].RelativePath.Should().Be("signalr-mock.fixture.ts");
    }

    [Fact]
    public async Task GenerateSignalRMockAsync_ShouldWriteFileToFileSystem()
    {
        // Act
        await _generator.GenerateSignalRMockAsync("/output");

        // Assert
        _fileSystem.WrittenFiles.Should().ContainKey("/output/signalr-mock.fixture.ts");
    }

    [Fact]
    public void Constructor_WithNullAnalyzer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<GeneratorOptions>>();
        optionsWrapper.Value.Returns(_options);

        // Act
        var act = () => new CodeGenerator(null!, _templateEngine, _fileSystem, _logger, optionsWrapper);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullTemplateEngine_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<GeneratorOptions>>();
        optionsWrapper.Value.Returns(_options);

        // Act
        var act = () => new CodeGenerator(_analyzer, null!, _fileSystem, _logger, optionsWrapper);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateForApplicationAsync_WithNullProject_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _generator.GenerateForApplicationAsync(null!, "/output");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateForApplicationAsync_WithNullOutputPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var project = CreateTestProject();

        // Act
        var act = () => _generator.GenerateForApplicationAsync(project, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static AngularProjectInfo CreateTestProject()
    {
        return new AngularProjectInfo
        {
            Name = "test-app",
            RootPath = "/app",
            SourceRoot = "/app/src",
            ProjectType = AngularProjectType.Application,
            Components =
            [
                new AngularComponentInfo
                {
                    Name = "LoginComponent",
                    Selector = "app-login",
                    FilePath = "/app/src/app/login/login.component.ts",
                    Selectors =
                    [
                        new ElementSelector
                        {
                            ElementType = "button",
                            Strategy = SelectorStrategy.TestId,
                            SelectorValue = "[data-testid='submit']",
                            PropertyName = "SubmitButton"
                        }
                    ]
                }
            ]
        };
    }

    private static AngularWorkspaceInfo CreateTestWorkspace()
    {
        return new AngularWorkspaceInfo
        {
            RootPath = "/workspace",
            DefaultProject = "app1",
            Projects =
            [
                new AngularProjectInfo
                {
                    Name = "app1",
                    RootPath = "/workspace/projects/app1",
                    SourceRoot = "/workspace/projects/app1/src",
                    ProjectType = AngularProjectType.Application,
                    Components = []
                }
            ]
        };
    }
}
