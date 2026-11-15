using System.Collections.Generic;
using GenLauncherGO.Features.Startup;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Provides local file-system operations for launcher-managed content.
/// </summary>
internal interface ILocalLauncherContentService
{
    /// <summary>
    ///     Finds installed content versions under the launcher-owned mods directory.
    /// </summary>
    IReadOnlyList<LauncherContentVersion> FindInstalledVersions(LauncherPaths paths);

    /// <summary>
    ///     Determines whether another program still holds one of a version's files open for writing, as a copy into the
    ///     mods directory does until each file is complete.
    /// </summary>
    bool IsVersionBeingWritten(
        LauncherPaths paths,
        LauncherContentKey contentKey);

    /// <summary>
    ///     Removes empty package-recovery directories that no longer contain a durable backup.
    /// </summary>
    void DeleteEmptyPackageBackupDirectories(LauncherPaths paths);

    /// <summary>
    ///     Deletes an installed content version from the launcher-owned mods directory.
    /// </summary>
    void DeleteVersion(
        LauncherPaths paths,
        LauncherContentKey contentKey);

    /// <summary>
    ///     Deletes all installed content files for a content card from the launcher-owned mods directory.
    /// </summary>
    void DeleteContent(
        LauncherPaths paths,
        LauncherContentKey contentKey);

    /// <summary>
    ///     Deletes the launcher-owned image cache when no content card still references the same content name.
    /// </summary>
    void DeleteImagesIfUnused(
        LauncherPaths paths,
        LauncherContentKey contentKey,
        LauncherData launcherData);
}
