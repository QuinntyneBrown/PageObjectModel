using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Provides file system operations.
/// </summary>
public sealed class FileSystemService : IFileSystem
{
    /// <inheritdoc />
    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return File.Exists(path);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public void CreateDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetFiles(string path, string pattern, bool recursive = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(pattern);

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(path, pattern, searchOption);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetDirectories(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Directory.GetDirectories(path);
    }

    /// <inheritdoc />
    public string CombinePath(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths);
    }

    /// <inheritdoc />
    public string GetFileName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetFileName(path);
    }

    /// <inheritdoc />
    public string GetFileNameWithoutExtension(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetFileNameWithoutExtension(path);
    }

    /// <inheritdoc />
    public string? GetDirectoryName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetDirectoryName(path);
    }

    /// <inheritdoc />
    public string GetFullPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetFullPath(path);
    }
}
