using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Core.Services;
using PlaywrightPomGenerator.Tests.TestUtilities;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class DistAnalyzerTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly DistAnalyzer _analyzer;

    public DistAnalyzerTests()
    {
        _analyzer = new DistAnalyzer(_fileSystem, Substitute.For<ILogger<DistAnalyzer>>());
    }

    [Fact]
    public async Task AnalyzeAsync_WithModernBrowserLayout_ShouldExtractBaseHrefAndPrerenderedRoutes()
    {
        // Arrange — Angular 17+ application-builder layout.
        _fileSystem.AddFile("/app/dist/shop/browser/index.html",
            "<html><head><base href=\"/portal/\"></head><body></body></html>");
        _fileSystem.AddFile("/app/dist/shop/browser/about/index.html", "<html></html>");
        _fileSystem.AddFile("/app/dist/shop/browser/products/list/index.html", "<html></html>");

        // Act
        var result = await _analyzer.AnalyzeAsync("/app", "shop");

        // Assert
        result.Should().NotBeNull();
        result!.DistPath.Should().Be("/app/dist/shop/browser");
        result.BaseHref.Should().Be("/portal/");
        result.PrerenderedRoutes.Should().BeEquivalentTo(["/about", "/products/list"]);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldProbeLayoutsInOrder()
    {
        // Classic dist/{project} layout (no browser/ subdirectory).
        _fileSystem.AddFile("/app/dist/shop/index.html", "<base href=\"/\">");

        var result = await _analyzer.AnalyzeAsync("/app", "shop");

        result!.DistPath.Should().Be("/app/dist/shop");
        result.BaseHref.Should().Be("/");
    }

    [Fact]
    public async Task AnalyzeAsync_WithFlatDist_ShouldUseIt()
    {
        _fileSystem.AddFile("/app/dist/index.html", "<html><head></head></html>");

        var result = await _analyzer.AnalyzeAsync("/app", "shop");

        result!.DistPath.Should().Be("/app/dist");
        result.BaseHref.Should().BeNull("no base tag is present");
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutDist_ShouldReturnNull()
    {
        _fileSystem.AddDirectory("/app/src");

        var result = await _analyzer.AnalyzeAsync("/app", "shop");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WithExplicitPath_ShouldUseItDirectly()
    {
        _fileSystem.AddDirectory("/elsewhere/build");
        _fileSystem.AddFile("/elsewhere/build/index.html", "<base href='/x/'>");

        var result = await _analyzer.AnalyzeAsync("/app", "shop", "/elsewhere/build");

        result!.DistPath.Should().Be("/elsewhere/build");
        result.BaseHref.Should().Be("/x/");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMissingExplicitPath_ShouldReturnNull()
    {
        var result = await _analyzer.AnalyzeAsync("/app", "shop", "/does/not/exist");

        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullFileSystem_ShouldThrowArgumentNullException()
    {
        var act = () => new DistAnalyzer(null!, Substitute.For<ILogger<DistAnalyzer>>());
        act.Should().Throw<ArgumentNullException>();
    }
}
