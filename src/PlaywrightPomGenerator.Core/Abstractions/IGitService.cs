using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Defines the contract for git operations using the git CLI.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Clones a repository to a temporary directory and returns the path.
    /// </summary>
    /// <param name="urlInfo">The parsed git URL information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the cloned repository.</returns>
    Task<string> CloneToTempAsync(GitUrlInfo urlInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the temporary repository directory.
    /// </summary>
    /// <param name="tempPath">The path to the temporary repository.</param>
    void CleanupTempRepo(string tempPath);

    /// <summary>
    /// Verifies that git is available on the system PATH.
    /// </summary>
    /// <returns>True if git is available, false otherwise.</returns>
    Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default);
}
