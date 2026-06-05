using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

public sealed class GenerateComponentCommandHandlerTests
{
    private readonly IAngularAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateComponentCommandHandler> _logger;
    private readonly GenerateComponentCommandHandler _handler;

    public GenerateComponentCommandHandlerTests()
    {
        _analyzer = Substitute.For<IAngularAnalyzer>();
        _generator = Substitute.For<ICodeGenerator>();
        _logger = Substitute.For<ILogger<GenerateComponentCommandHandler>>();
        _handler = new GenerateComponentCommandHandler(_analyzer, _generator, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WhenComponentsFound_ShouldCallGeneratorAndReturnZero()
    {
        // Arrange
        _analyzer.AnalyzeComponentsAtPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectWithComponents());
        _generator.GenerateComponentObjectsAsync(
                Arg.Any<AngularProjectInfo>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        var result = await _handler.ExecuteAsync("/app", null, false, CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _generator.Received(1).GenerateComponentObjectsAsync(
            Arg.Any<AngularProjectInfo>(),
            Arg.Any<string>(),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoComponentsFound_ShouldReturnError()
    {
        // Arrange
        _analyzer.AnalyzeComponentsAtPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateEmptyProject());

        // Act
        var result = await _handler.ExecuteAsync("/empty", null, false, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        await _generator.DidNotReceive().GenerateComponentObjectsAsync(
            Arg.Any<AngularProjectInfo>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomOutput_ShouldUseCustomPath()
    {
        // Arrange
        _analyzer.AnalyzeComponentsAtPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectWithComponents());
        _generator.GenerateComponentObjectsAsync(
                Arg.Any<AngularProjectInfo>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync("/app", "/custom/output", false, CancellationToken.None);

        // Assert
        await _generator.Received(1).GenerateComponentObjectsAsync(
            Arg.Any<AngularProjectInfo>(),
            "/custom/output",
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithExcludeRoutable_ShouldPassFlagToGenerator()
    {
        // Arrange
        _analyzer.AnalyzeComponentsAtPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectWithComponents());
        _generator.GenerateComponentObjectsAsync(
                Arg.Any<AngularProjectInfo>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync("/app", null, excludeRoutable: true, CancellationToken.None);

        // Assert
        await _generator.Received(1).GenerateComponentObjectsAsync(
            Arg.Any<AngularProjectInfo>(),
            Arg.Any<string>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenerationFails_ShouldReturnError()
    {
        // Arrange
        _analyzer.AnalyzeComponentsAtPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectWithComponents());
        _generator.GenerateComponentObjectsAsync(
                Arg.Any<AngularProjectInfo>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Failed("Test error"));

        // Act
        var result = await _handler.ExecuteAsync("/app", null, false, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _handler.ExecuteAsync(null!, null, false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAnalyzer_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateComponentCommandHandler(null!, _generator, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGenerator_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateComponentCommandHandler(_analyzer, null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateComponentCommandHandler(_analyzer, _generator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static AngularProjectInfo CreateProjectWithComponents()
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
                    Name = "KpiCardComponent",
                    Selector = "app-kpi-card",
                    FilePath = "/app/src/app/kpi-card/kpi-card.component.ts"
                }
            ]
        };
    }

    private static AngularProjectInfo CreateEmptyProject()
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
