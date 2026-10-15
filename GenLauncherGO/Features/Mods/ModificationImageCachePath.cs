using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.IO;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Owns safe cached-image paths and the file naming convention for each version's cached artwork and palette.
/// </summary>
internal static class ModificationImageCachePath
{
    public const string DirectorySubject = "Cached modification image directories";

    private const string PathSubject = "Cached modification image paths";

    private const string ImageDirectoryOwnerDescription = "the launcher-owned image directory";

    /// <summary>
    ///     Resolves a modification's cache folder when it exists, or returns <see langword="null" /> when it does not.
    /// </summary>
    /// <remarks>
    ///     Rejects a linked folder or linked entries inside it, so callers can enumerate and delete its files without
    ///     reaching outside the launcher-owned image directory.
    /// </remarks>
    public static string? ResolveExistingDirectory(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName)
    {
        string directory = ResolveDirectory(paths, modificationType, modificationName);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        FileSystemPathSafety.EnsureDirectoryTreeHasNoReparsePoints(directory, DirectorySubject);
        return directory;
    }

    /// <summary>
    ///     Creates a modification's cache folder when it is missing and returns it.
    /// </summary>
    /// <remarks>
    ///     Applies the same link checks as <see cref="ResolveExistingDirectory" />, so callers can write into the folder
    ///     without reaching outside the launcher-owned image directory.
    /// </remarks>
    public static string EnsureDirectory(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string directory = OwnedDirectoryTree.EnsureExists(
            paths.ImagesDirectory,
            ResolveDirectory(paths, modificationType, modificationName));
        FileSystemPathSafety.EnsureDirectoryTreeHasNoReparsePoints(directory, DirectorySubject);
        return directory;
    }

    /// <summary>
    ///     Builds the lexical cache path without traversing it, so cleanup can safely unlink a reparse-point entry.
    /// </summary>
    public static string GetDirectoryPath(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return paths.GetModificationImagesDirectory(GetDirectoryName(modificationType, modificationName));
    }

    public static string ResolvePath(LauncherPaths paths, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return FileSystemPathSafety.ResolveOwnedSubpath(
            paths.ImagesDirectory,
            imagePath,
            PathSubject,
            ImageDirectoryOwnerDescription);
    }

    public static string ResolveImagePath(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName,
        string imageFileName)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return ResolvePath(
            paths,
            paths.GetModificationImageFilePath(
                GetDirectoryName(modificationType, modificationName),
                imageFileName));
    }

    public static string ResolveRemoteImagePath(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName,
        string imageBaseName,
        Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return ResolveImagePath(
            paths,
            modificationType,
            modificationName,
            GetRemoteImageFileName(imageBaseName, sourceUri));
    }

    /// <summary>
    ///     Gets the content cards whose cached files share one content name's cache folder.
    /// </summary>
    /// <remarks>
    ///     The folder is named after the content alone, so cards of every content type with that name share it. Removing
    ///     the folder and pruning retired versions from it both decide ownership here; deciding it separately would let
    ///     one delete files the other still counts as owned.
    /// </remarks>
    public static IEnumerable<LauncherContent> GetFolderOwners(LauncherData data, string contentName)
    {
        ArgumentNullException.ThrowIfNull(data);

        return data.AllContent.Where(content => content.ContentKey.HasName(contentName));
    }

    /// <summary>
    ///     Builds the cache file name a version's tile image is stored under.
    /// </summary>
    public static string ResolveTileImageBaseName(string version)
    {
        return version;
    }

    /// <summary>
    ///     Builds the cache file name a version's shell artwork is stored under.
    /// </summary>
    public static string ResolveBackgroundImageBaseName(string version)
    {
        return version + "-background";
    }

    /// <summary>
    ///     Builds the cache file name a version's palette is stored under, so it survives an offline restart.
    /// </summary>
    /// <remarks>
    ///     The palette is cached beside the artwork it belongs to, which gives both the same lifetime: removing a
    ///     modification removes its cache folder and everything in it.
    /// </remarks>
    public static string ResolvePaletteBaseName(string version)
    {
        return version + "-theme";
    }

    /// <summary>
    ///     Gets every cache-file base name owned by one version: its tile image, its shell artwork, and its palette.
    /// </summary>
    /// <remarks>
    ///     The three builders above are the single authority for the names a version's cached files use. Writers,
    ///     readers, and catalog pruning only agree because they all call them, and pruning deletes every file this list
    ///     does not name, so a writer that drifted from these names would lose its file on the next catalog refresh. The
    ///     suffixes keep a version's shell artwork and palette clear of its own tile image only: the tile name is the
    ///     bare version, so another version's names can start with it (1.0.1-theme starts with 1.0.), and finding one
    ///     version's files must compare whole base names rather than prefixes.
    /// </remarks>
    public static IReadOnlyList<string> GetOwnedBaseNames(LauncherContentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return
        [
            ResolveTileImageBaseName(version.Version),
            ResolveBackgroundImageBaseName(version.Version),
            ResolvePaletteBaseName(version.Version)
        ];
    }

    private static string ResolveDirectory(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName)
    {
        return ResolvePath(paths, GetDirectoryPath(paths, modificationType, modificationName));
    }

    private static string GetRemoteImageFileName(string imageBaseName, Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);

        string extension = Path.GetExtension(sourceUri.LocalPath);
        if (!LauncherContentFileTypes.IsImage(extension))
        {
            extension = LauncherContentFileTypes.DefaultImageExtension;
        }

        return imageBaseName + extension;
    }

    private static string GetDirectoryName(ModificationType modificationType, string modificationName)
    {
        return modificationType == ModificationType.Advertising
            ? modificationName.Trim(Path.GetInvalidFileNameChars())
            : modificationName;
    }
}
