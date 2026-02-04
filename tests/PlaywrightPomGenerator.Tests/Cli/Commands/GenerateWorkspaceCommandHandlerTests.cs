using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

public sealed class GenerateWorkspaceCommandHandlerTests
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateWorkspaceCommandHandler> _logger;
    private readonly GenerateWorkspaceCommandHandler _handler;

    public GenerateWorkspaceCommandHandlerTests()
    {
        _analyzer = Substitute.For<IAngularAnalyzer>();
        _generator = Substitute.For<ICodeGenerator>();
        _logger = Substitute.For<ILogger<GenerateWorkspaceCommandHandler>>();
        _handler = new GenerateWorkspaceCommandHandler(_analyzer, _generator, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotValidWorkspace_ShouldReturnError()
    {
        // Arrange
        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(false);

        // Act
        var result = await _handler.ExecuteAsync("/invalid/path", null, null, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidWorkspace_ShouldCallGenerator()
    {
        // Arrange
        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(true);
        _analyzer.AnalyzeWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestWorkspace());
        _generator.GenerateForWorkspaceAsync(
            Arg.Any<AngularWorkspaceInfo>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        var result = await _handler.ExecuteAsync("/valid/workspace", null, null, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithProjectFilter_ShouldPassProjectName()
    {
        // Arrange
        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(true);
        _analyzer.AnalyzeWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestWorkspace());
        _generator.GenerateForWorkspaceAsync(
            Arg.Any<AngularWorkspaceInfo>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync("/workspace", null, "app1", CancellationToken.None);

        // Assert
        await _generator.Received(1).GenerateForWorkspaceAsync(
            Arg.Any<AngularWorkspaceInfo>(),
            Arg.Any<string>(),
            "app1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenerationFails_ShouldReturnError()
    {
        // Arrange
        _analyzer.IsWorkspace(Arg.Any<string>()).Returns(true);
        _analyzer.AnalyzeWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestWorkspace());
        _generator.GenerateForWorkspaceAsync(
            Arg.Any<AngularWorkspaceInfo>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Failed("Error"));

        // Act
        var result = await _handler.ExecuteAsync("/workspace", null, null, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _handler.ExecuteAsync(null!, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static AngularWorkspaceInfo CreateTestWorkspace()
    {
        return new AngularWorkspaceInfo
        {
            RootPath = "/workspace",
            Projects =
            [
                new AngularProjectInfo
                {
                    Name = "app1",
                    RootPath = "/workspace/app1",
                    SourceRoot = "/workspace/app1/src",
                    ProjectType = AngularProjectType.Application
                }
            ]
        };
    }
}
