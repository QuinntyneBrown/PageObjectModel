using System.Text.RegularExpressions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Parses git repository URLs from various hosting providers into structured information.
/// </summary>
public static partial class GitUrlParser
{
    /// <summary>
    /// Parses a git URL into its constituent parts.
    /// </summary>
    /// <param name="url">The git URL to parse.</param>
    /// <returns>Parsed URL information.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL cannot be parsed.</exception>
    public static GitUrlInfo Parse(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        url = url.Trim();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));
        }

        var host = uri.Host.ToLowerInvariant();

        return host switch
        {
            "github.com" or "www.github.com" => ParseGitHub(uri),
            "gitlab.com" or "www.gitlab.com" => ParseGitLab(uri),
            "bitbucket.org" or "www.bitbucket.org" => ParseBitbucket(uri),
            "dev.azure.com" => ParseAzureDevOps(uri),
            _ when host.Contains("gitlab") => ParseGitLab(uri),
            _ when HasGitLabPathPattern(uri) => ParseGitLab(uri),
            _ => ParseGeneric(uri)
        };
    }

    /// <summary>
    /// Attempts to parse a git URL, returning false if parsing fails.
    /// </summary>
    /// <param name="url">The git URL to parse.</param>
    /// <param name="result">The parsed URL information, or null if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string url, out GitUrlInfo? result)
    {
        try
        {
            result = Parse(url);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Detects GitLab-style URLs by path pattern (e.g., /-/blob/ or /-/tree/).
    /// This catches self-hosted GitLab instances whose hostname does not contain "gitlab".
    /// </summary>
    private static bool HasGitLabPathPattern(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.Contains("/-/blob/") || path.Contains("/-/tree/");
    }

    /// <summary>
    /// Determines whether a ref string looks like a commit hash rather than a branch name.
    /// </summary>
    private static bool IsCommitRef(string refValue)
    {
        if (string.IsNullOrEmpty(refValue) || refValue.Length < 7)
        {
            return false;
        }

        // Standard git SHA-1 hex hash (7-40 hex chars)
        if (refValue.Length <= 40 && refValue.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
        {
            return true;
        }

        // Longer alphanumeric strings with at least one digit and no separators
        // (catches non-standard hash formats or abbreviated refs)
        if (refValue.Length >= 20
            && refValue.All(char.IsLetterOrDigit)
            && refValue.Any(char.IsDigit)
            && !refValue.Any(char.IsUpper))
        {
            return true;
        }

        return false;
    }

    private static GitUrlInfo ParseGitHub(Uri uri)
    {
        // GitHub URL formats:
        // https://github.com/{owner}/{repo}
        // https://github.com/{owner}/{repo}.git
        // https://github.com/{owner}/{repo}/blob/{branch}/{path}
        // https://github.com/{owner}/{repo}/tree/{branch}/{path}
        var segments = GetPathSegments(uri);

        if (segments.Length < 2)
        {
            throw new ArgumentException($"Invalid GitHub URL: {uri}");
        }

        var owner = segments[0];
        var repo = segments[1].TrimSuffix(".git");
        var cloneUrl = $"https://github.com/{owner}/{repo}.git";

        string? branch = null;
        string? commit = null;
        string? pathInRepo = null;

        // /blob/{branch}/{path} or /tree/{branch}/{path}
        if (segments.Length >= 4 && (segments[2] == "blob" || segments[2] == "tree"))
        {
            var refValue = segments[3];
            ClassifyRef(refValue, out branch, out commit);
            if (segments.Length > 4)
            {
                pathInRepo = string.Join("/", segments[4..]);
            }
        }

        return new GitUrlInfo
        {
            CloneUrl = cloneUrl,
            Branch = branch,
            Commit = commit,
            PathInRepo = pathInRepo,
            Provider = GitProvider.GitHub,
            Owner = owner,
            RepoName = repo,
            IsFilePath = !string.IsNullOrEmpty(pathInRepo) && HasFileExtension(pathInRepo)
        };
    }

    private static GitUrlInfo ParseGitLab(Uri uri)
    {
        // GitLab URL formats:
        // https://gitlab.com/{owner}/{repo}
        // https://gitlab.com/{owner}/{repo}.git
        // https://gitlab.com/{owner}/{repo}/-/blob/{branch}/{path}
        // https://gitlab.com/{owner}/{repo}/-/tree/{branch}/{path}
        // Also supports nested groups: https://gitlab.com/{group}/{subgroup}/{repo}/...
        // Also supports self-hosted: https://gitscm.company.com/{owner}/{repo}/-/blob/{ref}/{path}
        var segments = GetPathSegments(uri);

        if (segments.Length < 2)
        {
            throw new ArgumentException($"Invalid GitLab URL: {uri}");
        }

        // Find the "-" separator to determine where the repo path ends
        var dashIndex = Array.IndexOf(segments, "-");

        string owner;
        string repo;
        string? branch = null;
        string? commit = null;
        string? pathInRepo = null;

        if (dashIndex >= 2)
        {
            // Everything before "-" is owner/repo (possibly nested groups)
            owner = string.Join("/", segments[..^(segments.Length - dashIndex + 1)].Length > 0
                ? segments[..(dashIndex - 1)]
                : segments[..1]);
            repo = segments[dashIndex - 1].TrimSuffix(".git");

            // After "-" comes blob/tree/{ref}/{path}
            if (dashIndex + 2 < segments.Length &&
                (segments[dashIndex + 1] == "blob" || segments[dashIndex + 1] == "tree"))
            {
                var refValue = segments[dashIndex + 2];
                ClassifyRef(refValue, out branch, out commit);
                if (dashIndex + 3 < segments.Length)
                {
                    pathInRepo = string.Join("/", segments[(dashIndex + 3)..]);
                }
            }
        }
        else
        {
            // No "-" separator, simple owner/repo format
            owner = segments[0];
            repo = segments[^1].TrimSuffix(".git");
        }

        var cloneUrl = $"https://{uri.Host}/{owner}/{repo}.git";

        return new GitUrlInfo
        {
            CloneUrl = cloneUrl,
            Branch = branch,
            Commit = commit,
            PathInRepo = pathInRepo,
            Provider = GitProvider.GitLab,
            Owner = owner,
            RepoName = repo,
            IsFilePath = !string.IsNullOrEmpty(pathInRepo) && HasFileExtension(pathInRepo)
        };
    }

    private static GitUrlInfo ParseBitbucket(Uri uri)
    {
        // Bitbucket URL formats:
        // https://bitbucket.org/{owner}/{repo}
        // https://bitbucket.org/{owner}/{repo}/src/{branch}/{path}
        var segments = GetPathSegments(uri);

        if (segments.Length < 2)
        {
            throw new ArgumentException($"Invalid Bitbucket URL: {uri}");
        }

        var owner = segments[0];
        var repo = segments[1].TrimSuffix(".git");
        var cloneUrl = $"https://bitbucket.org/{owner}/{repo}.git";

        string? branch = null;
        string? commit = null;
        string? pathInRepo = null;

        // /src/{branch}/{path}
        if (segments.Length >= 4 && segments[2] == "src")
        {
            var refValue = segments[3];
            ClassifyRef(refValue, out branch, out commit);
            if (segments.Length > 4)
            {
                pathInRepo = string.Join("/", segments[4..]);
            }
        }

        return new GitUrlInfo
        {
            CloneUrl = cloneUrl,
            Branch = branch,
            Commit = commit,
            PathInRepo = pathInRepo,
            Provider = GitProvider.Bitbucket,
            Owner = owner,
            RepoName = repo,
            IsFilePath = !string.IsNullOrEmpty(pathInRepo) && HasFileExtension(pathInRepo)
        };
    }

    private static GitUrlInfo ParseAzureDevOps(Uri uri)
    {
        // Azure DevOps URL formats:
        // https://dev.azure.com/{org}/{project}/_git/{repo}
        // https://dev.azure.com/{org}/{project}/_git/{repo}?path={path}&version=GB{branch}
        var segments = GetPathSegments(uri);

        if (segments.Length < 4 || segments[2] != "_git")
        {
            throw new ArgumentException($"Invalid Azure DevOps URL: {uri}");
        }

        var owner = segments[0]; // org
        var project = segments[1];
        var repo = segments[3].TrimSuffix(".git");
        var cloneUrl = $"https://dev.azure.com/{owner}/{project}/_git/{repo}";

        string? branch = null;
        string? commit = null;
        string? pathInRepo = null;

        // Parse query params for path and branch
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var path = query["path"];
        var version = query["version"];

        if (!string.IsNullOrEmpty(path))
        {
            pathInRepo = path.TrimStart('/');
        }

        if (!string.IsNullOrEmpty(version) && version.StartsWith("GB", StringComparison.Ordinal))
        {
            var refValue = version[2..]; // Remove "GB" prefix
            ClassifyRef(refValue, out branch, out commit);
        }

        return new GitUrlInfo
        {
            CloneUrl = cloneUrl,
            Branch = branch,
            Commit = commit,
            PathInRepo = pathInRepo,
            Provider = GitProvider.AzureDevOps,
            Owner = owner,
            RepoName = repo,
            IsFilePath = !string.IsNullOrEmpty(pathInRepo) && HasFileExtension(pathInRepo)
        };
    }

    private static GitUrlInfo ParseGeneric(Uri uri)
    {
        // Generic git URL: try to extract useful info
        var path = uri.AbsolutePath.TrimStart('/').TrimEnd('/');

        // If it ends with .git, it's a clone URL
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            var repoPath = path[..^4]; // Remove .git
            var parts = repoPath.Split('/');
            var repoName = parts[^1];
            var owner = parts.Length > 1 ? string.Join("/", parts[..^1]) : null;

            return new GitUrlInfo
            {
                CloneUrl = uri.ToString(),
                Provider = GitProvider.Generic,
                Owner = owner,
                RepoName = repoName
            };
        }

        // Try to treat as owner/repo with optional path
        var segments = GetPathSegments(uri);
        if (segments.Length >= 2)
        {
            var owner = segments[0];
            var repo = segments[1];
            var cloneUrl = $"{uri.Scheme}://{uri.Host}/{owner}/{repo}.git";

            return new GitUrlInfo
            {
                CloneUrl = cloneUrl,
                Provider = GitProvider.Generic,
                Owner = owner,
                RepoName = repo,
                PathInRepo = segments.Length > 2 ? string.Join("/", segments[2..]) : null
            };
        }

        throw new ArgumentException($"Cannot parse git URL: {uri}");
    }

    /// <summary>
    /// Classifies a ref value as either a branch name or a commit hash.
    /// </summary>
    private static void ClassifyRef(string refValue, out string? branch, out string? commit)
    {
        if (IsCommitRef(refValue))
        {
            branch = null;
            commit = refValue;
        }
        else
        {
            branch = refValue;
            commit = null;
        }
    }

    private static string[] GetPathSegments(Uri uri)
    {
        return uri.AbsolutePath
            .TrimStart('/')
            .TrimEnd('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool HasFileExtension(string path)
    {
        var lastSegment = path.Split('/')[^1];
        return lastSegment.Contains('.') && !lastSegment.StartsWith('.');
    }
}

/// <summary>
/// Extension methods for string operations.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Removes the specified suffix from the string if it ends with it.
    /// </summary>
    public static string TrimSuffix(this string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }
}
