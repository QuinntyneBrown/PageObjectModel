namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents parsed information from a git repository URL.
/// </summary>
public sealed record GitUrlInfo
{
    /// <summary>
    /// Gets the clone URL for the repository (HTTPS).
    /// </summary>
    public required string CloneUrl { get; init; }

    /// <summary>
    /// Gets the branch or tag name, if the ref in the URL is a branch or tag.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Gets the commit hash, if the ref in the URL is a commit rather than a branch.
    /// </summary>
    public string? Commit { get; init; }

    /// <summary>
    /// Gets the ref value (branch, tag, or commit) to checkout. Returns <see cref="Branch"/>
    /// if set, otherwise <see cref="Commit"/>.
    /// </summary>
    public string? Ref => Branch ?? Commit;

    /// <summary>
    /// Gets the path within the repository to the target file or folder.
    /// </summary>
    public string? PathInRepo { get; init; }

    /// <summary>
    /// Gets the git hosting provider.
    /// </summary>
    public required GitProvider Provider { get; init; }

    /// <summary>
    /// Gets the repository owner (user or organization).
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string? RepoName { get; init; }

    /// <summary>
    /// Gets whether the path points to a single file rather than a directory.
    /// </summary>
    public bool IsFilePath { get; init; }
}

/// <summary>
/// Known git hosting providers.
/// </summary>
public enum GitProvider
{
    /// <summary>
    /// GitHub (github.com).
    /// </summary>
    GitHub,

    /// <summary>
    /// GitLab (gitlab.com or self-hosted).
    /// </summary>
    GitLab,

    /// <summary>
    /// Bitbucket (bitbucket.org).
    /// </summary>
    Bitbucket,

    /// <summary>
    /// Azure DevOps (dev.azure.com).
    /// </summary>
    AzureDevOps,

    /// <summary>
    /// Generic/unknown git hosting.
    /// </summary>
    Generic
}
