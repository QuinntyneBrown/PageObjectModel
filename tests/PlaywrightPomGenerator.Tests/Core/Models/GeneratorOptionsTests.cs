using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Core.Models;

public sealed class GeneratorOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var options = new GeneratorOptions();

        // Assert
        options.FileHeader.Should().BeEmpty("because by default no header is included");
        options.TestFileSuffix.Should().Be("spec");
        options.ToolVersion.Should().Be("1.2.0");
        options.OutputDirectoryName.Should().Be("e2e");
        options.GenerateJsDocComments.Should().BeTrue();
        options.DefaultTimeout.Should().Be(30000);
        options.BaseUrlPlaceholder.Should().Be("http://localhost:4200");
    }

    [Fact]
    public void SectionName_ShouldBeGenerator()
    {
        // Assert
        GeneratorOptions.SectionName.Should().Be("Generator");
    }

    [Fact]
    public void FileHeader_WhenSet_CanContainPlaceholders()
    {
        // Arrange - by default, FileHeader is empty, but callers can set a custom header with placeholders
        var options = new GeneratorOptions
        {
            FileHeader = """
                /**
                 * @file {FileName}
                 * @date {GeneratedDate}
                 * @version {ToolVersion}
                 */
                """
        };

        // Assert - the header template can contain placeholders
        options.FileHeader.Should().Contain("{FileName}");
        options.FileHeader.Should().Contain("{GeneratedDate}");
        options.FileHeader.Should().Contain("{ToolVersion}");
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var options = new GeneratorOptions
        {
            FileHeader = "Custom Header",
            TestFileSuffix = "test",
            ToolVersion = "2.0.0",
            OutputDirectoryName = "tests",
            GenerateJsDocComments = false,
            DefaultTimeout = 60000,
            BaseUrlPlaceholder = "http://localhost:3000"
        };

        // Assert
        options.FileHeader.Should().Be("Custom Header");
        options.TestFileSuffix.Should().Be("test");
        options.ToolVersion.Should().Be("2.0.0");
        options.OutputDirectoryName.Should().Be("tests");
        options.GenerateJsDocComments.Should().BeFalse();
        options.DefaultTimeout.Should().Be(60000);
        options.BaseUrlPlaceholder.Should().Be("http://localhost:3000");
    }
}
