using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

public sealed class GenerateRemoteCommandHandlerTests
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly IGitService _gitService;
    private readonly ILogger<GenerateRemoteCommandHandler> _logger;
    private readonly GenerateRemoteCommandHandler _handler;

    public GenerateRemoteCommandHandlerTests()
    {
        _analyzer = Substitute.For<IAngularAnalyzer>();
        _generator = Substitute.For<ICodeGenerator>();
        _gitService = Substitute.For<IGitService>();
        _logger = Substitute.For<ILogger<GenerateRemoteCommandHandler>>();
        _handler = new GenerateRemoteCommandHandler(_analyzer, _generator, _gitService, _logger);
    }

    [Fact]
    public void Constructor_WithNullAnalyzer_ShouldThrowArgumentNullException()
    {
        var act = () => new GenerateRemoteCommandHandler(null!, _generator, _gitService, _logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGenerator_ShouldThrowArgumentNullException()
    {
        var act = () => new GenerateRemoteCommandHandler(_analyzer, null!, _gitService, _logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGitService_ShouldThrowArgumentNullException()
    {
        var act = () => new GenerateRemoteCommandHandler(_analyzer, _generator, null!, _logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new GenerateRemoteCommandHandler(_analyzer, _generator, _gitService, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullUrl_ShouldThrowArgumentNullException()
    {
        var act = () => _handler.ExecuteAsync(null!, null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenGitNotAvailable_ShouldReturnError()
    {
        // Arrange
        _gitService.IsGitAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.ExecuteAsync(
            "https://github.com/owner/repo", null, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidUrl_ShouldReturnError()
    {
        // Arrange
        _gitService.IsGitAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.ExecuteAsync("not-a-valid-url", null, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCloneFails_ShouldReturnError()
    {
        // Arrange
        _gitService.IsGitAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        _gitService.CloneToTempAsync(Arg.Any<GitUrlInfo>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("Clone failed"));

        // Act
        var result = await _handler.ExecuteAsync(
            "https://github.com/owner/repo", null, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        // CleanupTempRepo is not called because tempPath is null when clone throws
        _gitService.DidNotReceive().CleanupTempRepo(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithValidAngularApp_ShouldGenerateAndReturnSuccess()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "ppg-test");
        _gitService.IsGitAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        _gitService.CloneToTempAsync(Arg.Any<GitUrlInfo>(), Arg.Any<CancellationToken>())
            .Returns(tempPath);

        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(false);
        _analyzer.IsApplication(Arg.Any<string>()).Returns(true);
        _analyzer.IsLibrary(Arg.Any<string>()).Returns(false);
        _analyzer.AnalyzeApplicationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestProject());
        _generator.GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        var result = await _handler.ExecuteAsync(
            "https://github.com/owner/repo", "/output", CancellationToken.None);

        // Assert
        result.Should().Be(0);
        _gitService.Received(1).CleanupTempRepo(tempPath);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAlwaysCleanupTempRepo()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "ppg-test");
        _gitService.IsGitAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        _gitService.CloneToTempAsync(Arg.Any<GitUrlInfo>(), Arg.Any<CancellationToken>())
            .Returns(tempPath);

        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(false);
        _analyzer.IsApplication(Arg.Any<string>()).Returns(false);
        _analyzer.IsLibrary(Arg.Any<string>()).Returns(false);
        _analyzer.AnalyzeComponentsAtPathAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestProject());
        _generator.GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync(
            "https://github.com/owner/repo", null, CancellationToken.None);

        // Assert
        _gitService.Received(1).CleanupTempRepo(tempPath);
    }

    [Fact]
    public async Task ExecuteAsync_WithComponentPath_ShouldAnalyzeComponentsAtPath()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "ppg-test");
        _gitService.IsGitAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        _gitService.CloneToTempAsync(Arg.Any<GitUrlInfo>(), Arg.Any<CancellationToken>())
            .Returns(tempPath);

        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(false);
        _analyzer.IsApplication(Arg.Any<string>()).Returns(false);
        _analyzer.IsLibrary(Arg.Any<string>()).Returns(false);
        _analyzer.AnalyzeComponentsAtPathAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestProjectWithComponents());
        _generator.GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        var result = await _handler.ExecuteAsync(
            "https://github.com/owner/repo/blob/main/src/app/component.ts",
            "/output",
            CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GenerateRemoteCommand_ShouldHaveCorrectNameAndDescription()
    {
        // Act
        var command = new GenerateRemoteCommand();

        // Assert
        command.Name.Should().Be("remote");
        command.Description.Should().Contain("remote git repository");
    }

    [Fact]
    public void GenerateRemoteCommand_ShouldHaveUrlArgument()
    {
        // Act
        var command = new GenerateRemoteCommand();

        // Assert
        command.UrlArgument.Should().NotBeNull();
        command.UrlArgument.Name.Should().Be("url");
    }

    [Fact]
    public void GenerateRemoteCommand_ShouldHaveOutputOption()
    {
        // Act
        var command = new GenerateRemoteCommand();

        // Assert
        command.OutputOption.Should().NotBeNull();
        command.OutputOption.Name.Should().Be("output");
    }

    private static AngularProjectInfo CreateTestProject()
    {
        return new AngularProjectInfo
        {
            Name = "test-app",
            RootPath = "/app",
            SourceRoot = "/app/src",
            ProjectType = AngularProjectType.Application,
            Components = []
        };
    }

    private static AngularProjectInfo CreateTestProjectWithComponents()
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
                    Name = "TestComponent",
                    Selector = "app-test",
                    FilePath = "/app/src/test.component.ts"
                }
            ]
        };
    }
}
