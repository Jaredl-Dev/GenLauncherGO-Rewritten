using System;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.IO;

namespace GenLauncherGO.Features.Updating;

/// <summary>
///     Describes the explicit ownership boundaries for package staging, installed content, and durable recovery.
/// </summary>
internal sealed record PackageUpdatePathSet
{
    public PackageUpdatePathSet(
        OwnedContentPath temporaryPath,
        OwnedContentPath installedPath,
        OwnedContentPath backupPath,
        OwnedContentPath? latestInstalledPath = null)
    {
        TemporaryPath = temporaryPath ?? throw new ArgumentNullException(nameof(temporaryPath));
        InstalledPath = installedPath ?? throw new ArgumentNullException(nameof(installedPath));
        BackupPath = backupPath ?? throw new ArgumentNullException(nameof(backupPath));
        LatestInstalledPath = latestInstalledPath;
    }

    public OwnedContentPath TemporaryPath { get; }

    public OwnedContentPath InstalledPath { get; }

    public OwnedContentPath BackupPath { get; }

    public OwnedContentPath? LatestInstalledPath { get; }

    /// <summary>
    ///     Creates package paths from canonical launcher paths and a centrally resolved installed path.
    /// </summary>
    public static PackageUpdatePathSet Create(
        LauncherPaths launcherPaths,
        OwnedContentPath installedPath,
        OwnedContentPath? latestInstalledPath = null)
    {
        ArgumentNullException.ThrowIfNull(launcherPaths);
        ArgumentNullException.ThrowIfNull(installedPath);

        return new PackageUpdatePathSet(
            launcherPaths.GetPackageTemporaryPath(installedPath),
            installedPath,
            launcherPaths.GetPackageBackupPath(installedPath),
            latestInstalledPath);
    }

    /// <summary>
    ///     Creates the paths for installing a manual import, staged in the launcher's manual-import folder.
    /// </summary>
    public static PackageUpdatePathSet CreateForManualImport(
        LauncherPaths launcherPaths,
        OwnedContentPath installedPath)
    {
        ArgumentNullException.ThrowIfNull(launcherPaths);
        ArgumentNullException.ThrowIfNull(installedPath);

        return new PackageUpdatePathSet(
            new OwnedContentPath(launcherPaths.TempDirectory, launcherPaths.ManualImportStagingDirectory),
            installedPath,
            launcherPaths.GetPackageBackupPath(installedPath));
    }
}
