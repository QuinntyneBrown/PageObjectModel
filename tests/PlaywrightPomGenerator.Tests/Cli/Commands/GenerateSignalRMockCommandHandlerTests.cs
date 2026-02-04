using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

public sealed class GenerateSignalRMockCommandHandlerTests
{
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateSignalRMockCommandHandler> _logger;
    private readonly GenerateSignalRMockCommandHandler _handler;

    public GenerateSignalRMockCommandHandlerTests()
    {
        _generator = Substitute.For<ICodeGenerator>();
        _logger = Substitute.For<ILogger<GenerateSignalRMockCommandHandler>>();
        _handler = new GenerateSignalRMockCommandHandler(_generator, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ShouldReturnZero()
    {
        // Arrange
        _generator.GenerateSignalRMockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([
                new GeneratedFile
                {
                    RelativePath = "signalr-mock.fixture.ts",
                    AbsolutePath = "/output/signalr-mock.fixture.ts",
                    FileType = GeneratedFileType.SignalRMock,
                    Content = "// mock"
                }
            ]));

        // Act
        var result = await _handler.ExecuteAsync("/output", CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallGeneratorWithCorrectPath()
    {
        // Arrange
        _generator.GenerateSignalRMockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Successful([]));

        // Act
        await _handler.ExecuteAsync("/custom/output", CancellationToken.None);

        // Assert
        await _generator.Received(1).GenerateSignalRMockAsync(
            "/custom/output",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenerationFails_ShouldReturnOne()
    {
        // Arrange
        _generator.GenerateSignalRMockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GenerationResult.Failed("Generation failed"));

        // Act
        var result = await _handler.ExecuteAsync("/output", CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOutput_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _handler.ExecuteAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGenerator_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateSignalRMockCommandHandler(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GenerateSignalRMockCommandHandler(_generator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
