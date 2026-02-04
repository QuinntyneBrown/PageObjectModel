using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Core.Models;

public sealed class GenerationRequestTests
{
    [Fact]
    public void All_ShouldCreateRequestWithAllOptionsEnabled()
    {
        // Act
        var request = GenerationRequest.All("/path/to/app");

        // Assert
        request.TargetPath.Should().Be("/path/to/app");
        request.GenerateFixtures.Should().BeTrue();
        request.GenerateConfigs.Should().BeTrue();
        request.GenerateSelectors.Should().BeTrue();
        request.GeneratePageObjects.Should().BeTrue();
        request.GenerateHelpers.Should().BeTrue();
        request.HasAnyGenerationOption.Should().BeTrue();
    }

    [Fact]
    public void All_WithOutputPath_ShouldSetOutputPath()
    {
        // Act
        var request = GenerationRequest.All("/path/to/app", "/output/path");

        // Assert
        request.OutputPath.Should().Be("/output/path");
    }

    [Fact]
    public void HasAnyGenerationOption_WhenNoOptionsSet_ShouldReturnFalse()
    {
        // Arrange
        var request = new GenerationRequest
        {
            TargetPath = "/path"
        };

        // Assert
        request.HasAnyGenerationOption.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void HasAnyGenerationOption_WhenAnyOptionSet_ShouldReturnTrue(
        bool fixtures, bool configs, bool selectors, bool pageObjects, bool helpers)
    {
        // Arrange
        var request = new GenerationRequest
        {
            TargetPath = "/path",
            GenerateFixtures = fixtures,
            GenerateConfigs = configs,
            GenerateSelectors = selectors,
            GeneratePageObjects = pageObjects,
            GenerateHelpers = helpers
        };

        // Assert
        request.HasAnyGenerationOption.Should().BeTrue();
    }
}
