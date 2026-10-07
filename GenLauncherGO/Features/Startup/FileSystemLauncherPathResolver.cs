using System;
using System.IO;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Resolves and safely prepares standalone launcher-owned paths.
/// </summary>
internal sealed class FileSystemLauncherPathResolver : ILauncherPathResolver
{
    /// <summary>
    ///     The HRESULT of <c>ERROR_WRITE_PROTECT</c>, raised by a locked SD card or USB drive or one made read-only by
    ///     policy.
    /// </summary>
    /// <remarks>
    ///     Kept deliberately: people run the portable archive from removable drives, and moving the launcher is their
    ///     fix. Tests cannot create write-protected media, so this case has no automated coverage.
    /// </remarks>
    private const int ErrorWriteProtectHResult = unchecked((int)0x80070013);

    private readonly ILogger<FileSystemLauncherPathResolver> _logger;

    public FileSystemLauncherPathResolver()
        : this(NullLogger<FileSystemLauncherPathResolver>.Instance)
    {
    }

    public FileSystemLauncherPathResolver(ILogger<FileSystemLauncherPathResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public LauncherStoragePaths Resolve(string executableDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);

        return new LauncherStoragePaths(executableDirectory);
    }

    public bool TryPrepareLauncherDirectories(LauncherStoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        try
        {
            OwnedDirectoryTree.EnsureExists(paths.ExecutableDirectory, paths.DataDirectory);
            OwnedDirectoryTree.EnsureExists(paths.DataDirectory, paths.LogsDirectory);

            // Only a real file proves access: permissions alone miss Controlled Folder Access, share rights,
            // inherited Deny entries, and write-protected media. Windows deletes the file on close.
            new FileStream(
                Path.Combine(paths.DataDirectory, $".write-test-{Guid.NewGuid():N}.tmp"),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose).Dispose();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                              or IOException { HResult: ErrorWriteProtectHResult })
        {
            return false;
        }

        _logger.LogDebug("Prepared shared standalone launcher directories.");
        return true;
    }

    public void PrepareGameDirectories(LauncherPaths paths, bool cleanTemporaryDirectory)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string dataDirectory = Path.GetDirectoryName(paths.OwnedGameDataDirectory)
                               ?? throw new InvalidDataException(
                                   "A per-game data directory must have an owning shared data directory.");
        OwnedDirectoryTree.EnsureExists(dataDirectory, paths.OwnedGameDataDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.RuntimeDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.CacheDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.ImagesDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.ModsDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.TempDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.DeploymentDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.IntegrityDirectory);
        OwnedDirectoryTree.EnsureExists(paths.OwnedGameDataDirectory, paths.StateDirectory);

        if (cleanTemporaryDirectory)
        {
            // Staged packages are kept: a download the launcher suspended on close leaves its partial content
            // here, and that content is exactly what lets the next session resume instead of starting over.
            OwnedDirectoryTree.PrepareEmptyExcept(
                paths.OwnedGameDataDirectory,
                paths.TempDirectory,
                paths.PackagesDirectory);
        }

        _logger.LogDebug(
            "Prepared isolated launcher directories for {SupportedGame}.",
            paths.Game);
    }
}
