using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Core.Models;

public sealed class GenerationResultTests
{
    [Fact]
    public void Successful_WithFiles_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var files = new List<GeneratedFile>
        {
            new()
            {
                RelativePath = "test.ts",
                AbsolutePath = "/full/path/test.ts",
                FileType = GeneratedFileType.PageObject,
                Content = "content"
            }
        };

        // Act
        var result = GenerationResult.Successful(files);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Successful_WithWarnings_ShouldIncludeWarnings()
    {
        // Arrange
        var files = new List<GeneratedFile>();
        var warnings = new List<string> { "Warning 1", "Warning 2" };

        // Act
        var result = GenerationResult.Successful(files, warnings);

        // Assert
        result.Success.Should().BeTrue();
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("Warning 1");
        result.Warnings.Should().Contain("Warning 2");
    }

    [Fact]
    public void Failed_WithErrors_ShouldCreateFailedResult()
    {
        // Arrange
        var errors = new List<string> { "Error 1", "Error 2" };

        // Act
        var result = GenerationResult.Failed(errors);

        // Assert
        result.Success.Should().BeFalse();
        result.GeneratedFiles.Should().BeEmpty();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain("Error 1");
    }

    [Fact]
    public void Failed_WithSingleError_ShouldCreateFailedResult()
    {
        // Act
        var result = GenerationResult.Failed("Single error");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors.Should().Contain("Single error");
    }
}
