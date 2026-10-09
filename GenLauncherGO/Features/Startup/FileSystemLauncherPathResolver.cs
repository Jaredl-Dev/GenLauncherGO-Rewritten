using System;
using System.IO;
using GenLauncherGO.Shared.IO;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Resolves and safely prepares standalone launcher-owned paths.
/// </summary>
internal sealed class FileSystemLauncherPathResolver : ILauncherPathResolver
{
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
        }
        catch (Exception exception) when (DirectoryWriteProbe.IsWriteDenied(exception))
        {
            return false;
        }

        return DirectoryWriteProbe.CanCreateFiles(paths.DataDirectory);
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
    }
}
