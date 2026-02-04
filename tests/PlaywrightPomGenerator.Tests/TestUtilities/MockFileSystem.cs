using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Tests.TestUtilities;

/// <summary>
/// Mock implementation of IFileSystem for testing.
/// </summary>
public sealed class MockFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the files that have been written.
    /// </summary>
    public IReadOnlyDictionary<string, string> WrittenFiles => _files;

    /// <summary>
    /// Gets the directories that have been created.
    /// </summary>
    public IReadOnlyCollection<string> CreatedDirectories => _directories;

    /// <summary>
    /// Adds a file to the mock file system.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="content">The file content.</param>
    public void AddFile(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = content;

        // Also add parent directories
        var directory = GetDirectoryName(normalizedPath);
        while (!string.IsNullOrEmpty(directory))
        {
            _directories.Add(directory);
            directory = GetDirectoryName(directory);
        }
    }

    /// <summary>
    /// Adds a directory to the mock file system.
    /// </summary>
    /// <param name="path">The directory path.</param>
    public void AddDirectory(string path)
    {
        _directories.Add(NormalizePath(path));
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var content))
        {
            return Task.FromResult(content);
        }
        throw new FileNotFoundException($"File not found: {path}");
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = content;

        // Create parent directory if needed
        var directory = GetDirectoryName(normalizedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            _directories.Add(directory);
        }

        return Task.CompletedTask;
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(NormalizePath(path));
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(NormalizePath(path));
    }

    public void CreateDirectory(string path)
    {
        _directories.Add(NormalizePath(path));
    }

    public IEnumerable<string> GetFiles(string path, string pattern, bool recursive = false)
    {
        var normalizedPath = NormalizePath(path);
        var searchPattern = pattern.Replace("*", "").Replace(".", "\\.");

        return _files.Keys
            .Where(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                if (!recursive)
                {
                    var relativePath = f[(normalizedPath.Length + 1)..];
                    if (relativePath.Contains('/') || relativePath.Contains('\\'))
                    {
                        return false;
                    }
                }
                return MatchesPattern(f, pattern);
            });
    }

    public IEnumerable<string> GetDirectories(string path)
    {
        var normalizedPath = NormalizePath(path);
        return _directories
            .Where(d => d.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase) ||
                       d.StartsWith(normalizedPath + "\\", StringComparison.OrdinalIgnoreCase))
            .Where(d =>
            {
                var relativePath = d[(normalizedPath.Length + 1)..];
                return !relativePath.Contains('/') && !relativePath.Contains('\\');
            });
    }

    public string CombinePath(params string[] paths)
    {
        return Path.Combine(paths).Replace('\\', '/');
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }

    public string GetFileNameWithoutExtension(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    public string? GetDirectoryName(string path)
    {
        var result = Path.GetDirectoryName(path);
        return result?.Replace('\\', '/');
    }

    public string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return NormalizePath(path);
        }
        return NormalizePath(Path.Combine("/test", path));
    }

    private static string NormalizePath(string path)
    {
        // Replace backslashes with forward slashes
        var normalized = path.Replace('\\', '/').TrimEnd('/');

        // Track if path is absolute
        var isAbsolute = normalized.StartsWith("/");

        // Resolve ./ and ../ segments
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                // Skip current directory references
                continue;
            }

            if (segment == ".." && result.Count > 0 && result[^1] != "..")
            {
                // Go up one directory
                result.RemoveAt(result.Count - 1);
            }
            else if (segment != "..")
            {
                result.Add(segment);
            }
        }

        // Preserve leading slash for absolute paths
        var prefix = isAbsolute ? "/" : "";
        return prefix + string.Join("/", result);
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        // Simple pattern matching for common cases
        if (pattern == "*")
        {
            return true;
        }

        if (pattern.StartsWith("*"))
        {
            var suffix = pattern[1..];
            return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return fileName.Contains(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
