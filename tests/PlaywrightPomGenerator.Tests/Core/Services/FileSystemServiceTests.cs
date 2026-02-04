using FluentAssertions;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class FileSystemServiceTests
{
    private readonly FileSystemService _service = new();

    [Fact]
    public void CombinePath_ShouldCombineMultiplePaths()
    {
        // Act
        var result = _service.CombinePath("path", "to", "file.txt");

        // Assert
        result.Should().Contain("path");
        result.Should().Contain("to");
        result.Should().Contain("file.txt");
    }

    [Fact]
    public void GetFileName_ShouldReturnFileName()
    {
        // Arrange
        var path = Path.Combine("some", "path", "file.txt");

        // Act
        var result = _service.GetFileName(path);

        // Assert
        result.Should().Be("file.txt");
    }

    [Fact]
    public void GetFileNameWithoutExtension_ShouldReturnNameWithoutExtension()
    {
        // Arrange
        var path = Path.Combine("some", "path", "file.component.ts");

        // Act
        var result = _service.GetFileNameWithoutExtension(path);

        // Assert
        result.Should().Be("file.component");
    }

    [Fact]
    public void GetDirectoryName_ShouldReturnParentDirectory()
    {
        // Arrange
        var path = Path.Combine("some", "path", "file.txt");

        // Act
        var result = _service.GetDirectoryName(path);

        // Assert
        result.Should().Contain("some");
        result.Should().Contain("path");
        result.Should().NotContain("file.txt");
    }

    [Fact]
    public void GetFullPath_ShouldReturnAbsolutePath()
    {
        // Act
        var result = _service.GetFullPath("relative/path");

        // Assert
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void CombinePath_WithNull_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.CombinePath(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetFileName_WithNull_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.GetFileName(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FileExists_WithNull_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.FileExists(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DirectoryExists_WithNull_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.DirectoryExists(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateDirectory_WithNull_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.CreateDirectory(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
