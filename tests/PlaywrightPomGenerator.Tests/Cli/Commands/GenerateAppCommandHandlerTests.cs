using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

public sealed class GenerateAppCommandHandlerTests
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateAppCommandHandler> _logger;
    private readonly GenerateAppCommandHandler _handler;

    public GenerateAppCommandHandlerTests()
    {
        _analyzer = Substitute.For<IAngularAnalyzer>();
        _generator = Substitute.For<ICodeGenerator>();
        _logger = Substitute.For<ILogger<GenerateAppCommandHandler>>();
        _handler = new GenerateAppCommandHandler(_analyzer, _generator, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotValidApplication_ShouldReturnError()
    {
        // Arrange
        _analyzer.IsApplication(Arg.Any<string>()).Returns(false);

        // Act
        var result = await _handler.ExecuteAsync("/invalid/path", null, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidApplication_ShouldCallGenerator()
    {
        // Arrange
        _analyzer.IsApplication(Arg.Any<string>()).Returns(true);
        _analyzer.AnalyzeApplicationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestProject());
        _generator.GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        var result = await _handler.ExecuteAsync("/valid/path", null, CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _generator.Received(1).GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomOutput_ShouldUseCustomPath()
    {
        // Arrange
        _analyzer.IsApplication(Arg.Any<string>()).Returns(true);
        _analyzer.AnalyzeApplicationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestProject());
        _generator.GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync("/app", "/custom/output", CancellationToken.None);

        // Assert
        await _generator.Received(1).GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(),
            "/custom/output",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenerationFails_ShouldReturnError()
    {
        // Arrange
        _analyzer.IsApplication(Arg.Any<string>()).Returns(true);
        _analyzer.AnalyzeApplicationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateTestProject());
        _generator.GenerateForApplicationAsync(
            Arg.Any<AngularProjectInfo>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Failed("Test error"));

        // Act
        var result = await _handler.ExecuteAsync("/app", null, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _handler.ExecuteAsync(null!, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAnalyzer_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateAppCommandHandler(null!, _generator, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGenerator_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateAppCommandHandler(_analyzer, null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateAppCommandHandler(_analyzer, _generator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
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
}
