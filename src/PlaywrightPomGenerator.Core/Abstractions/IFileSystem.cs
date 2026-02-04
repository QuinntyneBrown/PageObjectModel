namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Defines the contract for file system operations.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents.</returns>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes text to a file, creating the file if it doesn't exist.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>True if the directory exists, false otherwise.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Creates a directory if it doesn't exist.
    /// </summary>
    /// <param name="path">The directory path.</param>
    void CreateDirectory(string path);

    /// <summary>
    /// Gets all files in a directory matching the specified pattern.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="pattern">The search pattern.</param>
    /// <param name="recursive">Whether to search recursively.</param>
    /// <returns>The matching file paths.</returns>
    IEnumerable<string> GetFiles(string path, string pattern, bool recursive = false);

    /// <summary>
    /// Gets all directories in a directory.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The directory paths.</returns>
    IEnumerable<string> GetDirectories(string path);

    /// <summary>
    /// Combines path segments.
    /// </summary>
    /// <param name="paths">The path segments.</param>
    /// <returns>The combined path.</returns>
    string CombinePath(params string[] paths);

    /// <summary>
    /// Gets the file name from a path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file name.</returns>
    string GetFileName(string path);

    /// <summary>
    /// Gets the file name without extension from a path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file name without extension.</returns>
    string GetFileNameWithoutExtension(string path);

    /// <summary>
    /// Gets the directory name from a path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The directory name.</returns>
    string? GetDirectoryName(string path);

    /// <summary>
    /// Gets the full path from a relative path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The full path.</returns>
    string GetFullPath(string path);
}
