using System;
using System.IO;
using System.Linq;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.IO;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Resolves launcher-owned content paths from canonical content identities.
/// </summary>
internal static class LauncherContentPathResolver
{
    private static readonly (ModificationType Type, string FolderName)[] _childContentFolders =
    [
        (ModificationType.Addon, LauncherFileSystemLayout.AddonsFolderName),
        (ModificationType.Patch, LauncherFileSystemLayout.PatchesFolderName)
    ];

    /// <summary>
    ///     Resolves the owned installed version directory, or returns <see langword="null" /> for an incomplete or
    ///     unsupported identity.
    /// </summary>
    public static OwnedContentPath? ResolveVersionPath(
        LauncherPaths paths,
        LauncherContentKey contentKey)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (string.IsNullOrWhiteSpace(contentKey.Version))
        {
            return null;
        }

        OwnedContentPath? contentPath = ResolveContentPath(paths, contentKey);
        if (contentPath is null)
        {
            return null;
        }

        string safeVersion = LexicalPath.NormalizePathSegment(contentKey.Version, nameof(contentKey.Version));
        string versionPath = LexicalPath.ResolvePath(contentPath.FullPath, safeVersion);
        return new OwnedContentPath(contentPath.OwnerRoot, versionPath);
    }

    /// <summary>
    ///     Resolves the owned content-card directory, or returns <see langword="null" /> for an incomplete identity.
    /// </summary>
    public static OwnedContentPath? ResolveContentPath(
        LauncherPaths paths,
        LauncherContentKey contentKey)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string[]? pathSegments = ResolveContentPathSegments(contentKey);
        return pathSegments is null
            ? null
            : ResolvePackagePath(paths.ModsDirectory, pathSegments);
    }

    /// <summary>
    ///     Resolves the owned cleanup root, or returns <see langword="null" /> for an incomplete identity.
    /// </summary>
    public static OwnedContentPath? ResolveCleanupRootPath(
        LauncherPaths paths,
        LauncherContentKey contentKey)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string[]? pathSegments = ResolveContentPathSegments(contentKey);
        return pathSegments is null
            ? null
            : ResolvePackagePath(paths.ModsDirectory, pathSegments[0]);
    }

    public static ModificationType? ResolveChildContentType(string folderName)
    {
        foreach ((ModificationType type, string name) in _childContentFolders)
        {
            if (string.Equals(folderName, name, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return null;
    }

    private static string[]? ResolveContentPathSegments(LauncherContentKey contentKey)
    {
        if (string.IsNullOrWhiteSpace(contentKey.Name))
        {
            return null;
        }

        if (contentKey.ContentType == ModificationType.Mod)
        {
            return [contentKey.Name];
        }

        if (string.IsNullOrWhiteSpace(contentKey.ParentIdentity))
        {
            return null;
        }

        foreach ((ModificationType type, string folderName) in _childContentFolders)
        {
            if (contentKey.ContentType == type)
            {
                return [contentKey.ParentIdentity, folderName, contentKey.Name];
            }
        }

        return null;
    }

    private static OwnedContentPath ResolvePackagePath(string modsDirectory, params string?[] segments)
    {
        string[] safeSegments = segments
            .Select((segment, index) => LexicalPath.NormalizePathSegment(segment, $"segment{index}"))
            .ToArray();
        string fullPath = LexicalPath.ResolvePath(modsDirectory, Path.Combine(safeSegments));
        return new OwnedContentPath(modsDirectory, fullPath);
    }
}
