using System;
using System.IO;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.IO;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Owns safe cached-image paths and the file naming convention for catalog artwork.
/// </summary>
internal static class ModificationImageCachePath
{
    private const string PathSubject = "Cached modification image paths";

    private const string ImageDirectoryOwnerDescription = "the launcher-owned image directory";

    public static string ResolveDirectory(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName)
    {
        return ResolvePath(paths, GetDirectoryPath(paths, modificationType, modificationName));
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
