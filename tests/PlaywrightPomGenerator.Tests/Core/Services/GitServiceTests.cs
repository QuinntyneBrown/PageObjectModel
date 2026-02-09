using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class GitServiceTests
{
    private readonly ILogger<GitService> _logger;
    private readonly GitService _service;

    public GitServiceTests()
    {
        _logger = Substitute.For<ILogger<GitService>>();
        _service = new GitService(_logger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new GitService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task IsGitAvailableAsync_ShouldReturnTrue_WhenGitIsInstalled()
    {
        // Act
        var result = await _service.IsGitAvailableAsync();

        // Assert
        // This test depends on git being installed on the CI/dev machine
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CloneToTempAsync_WithNullUrlInfo_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.CloneToTempAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void CleanupTempRepo_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.CleanupTempRepo(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CleanupTempRepo_WithNonExistentPath_ShouldNotThrow()
    {
        // Act
        var act = () => _service.CleanupTempRepo("/does/not/exist");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void CleanupTempRepo_WithExistingDirectory_ShouldDeleteIt()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "ppg-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempPath);
        File.WriteAllText(Path.Combine(tempPath, "test.txt"), "test");

        // Act
        _service.CleanupTempRepo(tempPath);

        // Assert
        Directory.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public async Task CloneToTempAsync_WithInvalidUrl_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var urlInfo = new GitUrlInfo
        {
            CloneUrl = "https://github.com/nonexistent-user-12345/nonexistent-repo-12345.git",
            Provider = GitProvider.GitHub,
            Owner = "nonexistent-user-12345",
            RepoName = "nonexistent-repo-12345"
        };

        // Act
        var act = () => _service.CloneToTempAsync(urlInfo);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to clone*");
    }
}
