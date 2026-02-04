using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

public sealed class GenerateArtifactsCommandHandlerTests
{
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateArtifactsCommandHandler> _logger;
    private readonly GenerateArtifactsCommandHandler _handler;

    public GenerateArtifactsCommandHandlerTests()
    {
        _generator = Substitute.For<ICodeGenerator>();
        _logger = Substitute.For<ILogger<GenerateArtifactsCommandHandler>>();
        _handler = new GenerateArtifactsCommandHandler(_generator, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoOptions_ShouldReturnError()
    {
        // Act
        var result = await _handler.ExecuteAsync(
            "/path", null, null, false, false, false, false, false, false, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithAllOption_ShouldEnableAllOptions()
    {
        // Arrange
        _generator.GenerateArtifactsAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync(
            "/path", null, null, false, false, false, false, false, true, CancellationToken.None);

        // Assert
        await _generator.Received(1).GenerateArtifactsAsync(
            Arg.Is<GenerationRequest>(r =>
                r.GenerateFixtures &&
                r.GenerateConfigs &&
                r.GenerateSelectors &&
                r.GeneratePageObjects &&
                r.GenerateHelpers),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithIndividualOptions_ShouldPassCorrectRequest()
    {
        // Arrange
        _generator.GenerateArtifactsAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync(
            "/path", "/output", "myproject",
            fixtures: true, configs: false, selectors: true, pageObjects: false, helpers: false, all: false,
            CancellationToken.None);

        // Assert
        await _generator.Received(1).GenerateArtifactsAsync(
            Arg.Is<GenerationRequest>(r =>
                r.TargetPath == "/path" &&
                r.OutputPath == "/output" &&
                r.ProjectName == "myproject" &&
                r.GenerateFixtures &&
                !r.GenerateConfigs &&
                r.GenerateSelectors &&
                !r.GeneratePageObjects &&
                !r.GenerateHelpers),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenerationSucceeds_ShouldReturnZero()
    {
        // Arrange
        _generator.GenerateArtifactsAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([
                new GeneratedFile
                {
                    RelativePath = "test.ts",
                    AbsolutePath = "/full/test.ts",
                    FileType = GeneratedFileType.Fixture,
                    Content = "content"
                }
            ]));

        // Act
        var result = await _handler.ExecuteAsync(
            "/path", null, null, true, false, false, false, false, false, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenerationFails_ShouldReturnOne()
    {
        // Arrange
        _generator.GenerateArtifactsAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Failed("Error message"));

        // Act
        var result = await _handler.ExecuteAsync(
            "/path", null, null, true, false, false, false, false, false, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _handler.ExecuteAsync(
            null!, null, null, true, false, false, false, false, false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGenerator_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateArtifactsCommandHandler(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateArtifactsCommandHandler(_generator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
