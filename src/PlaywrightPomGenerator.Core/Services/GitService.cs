using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Provides git operations using the git command-line interface.
/// </summary>
public sealed class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, _, _) = await RunGitAsync("--version", workingDirectory: null, cancellationToken)
                .ConfigureAwait(false);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string> CloneToTempAsync(GitUrlInfo urlInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(urlInfo);

        var tempPath = Path.Combine(Path.GetTempPath(), "ppg-" + Guid.NewGuid().ToString("N")[..8]);

        _logger.LogInformation("Cloning {CloneUrl} to {TempPath}", urlInfo.CloneUrl, tempPath);

        // Build clone arguments
        var cloneArgs = BuildCloneArguments(urlInfo, tempPath);

        var (exitCode, stdout, stderr) = await RunGitAsync(cloneArgs, workingDirectory: null, cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            // Clean up if clone failed
            CleanupTempRepo(tempPath);
            throw new InvalidOperationException(
                $"Failed to clone repository. Exit code: {exitCode}. Error: {stderr}");
        }

        _logger.LogInformation("Successfully cloned repository to {TempPath}", tempPath);

        // If a commit was specified, checkout that specific commit
        if (!string.IsNullOrEmpty(urlInfo.Commit))
        {
            _logger.LogInformation("Checking out commit {Commit}", urlInfo.Commit);

            var (checkoutExit, _, checkoutErr) = await RunGitAsync(
                $"checkout {urlInfo.Commit}", tempPath, cancellationToken)
                .ConfigureAwait(false);

            if (checkoutExit != 0)
            {
                CleanupTempRepo(tempPath);
                throw new InvalidOperationException(
                    $"Failed to checkout commit '{urlInfo.Commit}'. Error: {checkoutErr}");
            }
        }

        return tempPath;
    }

    /// <inheritdoc />
    public void CleanupTempRepo(string tempPath)
    {
        ArgumentNullException.ThrowIfNull(tempPath);

        if (!Directory.Exists(tempPath))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Cleaning up temporary repository at {TempPath}", tempPath);

            // Remove read-only attributes that git sets on .git objects
            RemoveReadOnlyAttributes(tempPath);
            Directory.Delete(tempPath, recursive: true);

            _logger.LogInformation("Successfully cleaned up {TempPath}", tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temporary repository at {TempPath}", tempPath);
        }
    }

    private static string BuildCloneArguments(GitUrlInfo urlInfo, string targetPath)
    {
        var args = new List<string> { "clone" };

        if (!string.IsNullOrEmpty(urlInfo.Commit))
        {
            // For commits, don't use --depth 1 or -b since the commit may not
            // be reachable from HEAD in a shallow clone, and -b only works with branches/tags
        }
        else
        {
            // Use shallow clone for efficiency when targeting a branch
            args.Add("--depth");
            args.Add("1");

            if (!string.IsNullOrEmpty(urlInfo.Branch))
            {
                args.Add("-b");
                args.Add(urlInfo.Branch);
            }
        }

        // Clone URL and target path
        args.Add(urlInfo.CloneUrl);
        args.Add($"\"{targetPath}\"");

        return string.Join(" ", args);
    }

    private async Task<bool> IsBranchOrTagAsync(
        string repoPath, string refName, CancellationToken cancellationToken)
    {
        var (exitCode, _, _) = await RunGitAsync(
            $"rev-parse --verify refs/heads/{refName}", repoPath, cancellationToken)
            .ConfigureAwait(false);

        if (exitCode == 0) return true;

        var (tagExitCode, _, _) = await RunGitAsync(
            $"rev-parse --verify refs/tags/{refName}", repoPath, cancellationToken)
            .ConfigureAwait(false);

        return tagExitCode == 0;
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunGitAsync(
        string arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        _logger.LogDebug("Running: git {Arguments}", arguments);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("git exit code: {ExitCode}", process.ExitCode);

        return (process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    private static void RemoveReadOnlyAttributes(string directoryPath)
    {
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }
    }
}
