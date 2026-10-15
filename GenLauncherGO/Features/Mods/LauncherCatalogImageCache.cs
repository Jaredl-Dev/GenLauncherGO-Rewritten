using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.IO;
using GenLauncherGO.Shared.Remote;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Caches the assets a remote launcher catalog publishes: tile art, shell artwork, and palettes.
/// </summary>
/// <remarks>
///     Everything cached here shares one lifetime, because it all lands in the modification's own cache folder and is
///     removed with the content card. Files a version leaves behind are pruned once the catalog stops listing it and the
///     user no longer holds it.
/// </remarks>
internal sealed class LauncherCatalogImageCache
{
    private readonly IRemoteAssetDownloader _assetDownloader;

    private readonly ILogger<LauncherCatalogImageCache> _logger;

    private readonly IModificationThemeCache _themeCache;

    public LauncherCatalogImageCache(
        IRemoteAssetDownloader assetDownloader,
        IModificationThemeCache themeCache,
        ILogger<LauncherCatalogImageCache> logger)
    {
        _assetDownloader = assetDownloader ?? throw new ArgumentNullException(nameof(assetDownloader));
        _themeCache = themeCache ?? throw new ArgumentNullException(nameof(themeCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CacheModificationImagesAsync(
        LauncherContentVersion modification,
        LauncherPaths paths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modification);
        ArgumentNullException.ThrowIfNull(paths);

        if (!string.IsNullOrWhiteSpace(modification.UIImageSourceLink))
        {
            await DownloadImageIfMissingAsync(
                paths,
                modification.ModificationType,
                modification.Name,
                ModificationImageCachePath.ResolveTileImageBaseName(modification.Version),
                modification.UIImageSourceLink,
                cancellationToken).ConfigureAwait(false);
        }

        if (modification.Theme is null)
        {
            return;
        }

        // Cached ahead of selection so a themed modification can re-skin the shell the moment it is picked, and
        // so a later offline run still has both the palette and the artwork it refers to.
        _themeCache.Save(modification.ContentKey, modification.Theme);

        if (modification.Theme.HasBackgroundImage)
        {
            await DownloadImageIfMissingAsync(
                paths,
                modification.ModificationType,
                modification.Name,
                ModificationImageCachePath.ResolveBackgroundImageBaseName(modification.Version),
                modification.Theme.GenLauncherBackgroundImageLink,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CacheAdvertisingImagesAsync(
        RemoteAdvertisingReference advertising,
        LauncherPaths paths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(advertising);
        ArgumentNullException.ThrowIfNull(paths);

        // Advertising images are cached by index, so a changed image count leaves no index safe to keep.
        DeleteStaleImages(
            paths,
            ModificationType.Advertising,
            advertising.Name,
            images => images.Length == advertising.ImageUrls.Count ? [] : images);

        var imageDownloads = new List<Task>(advertising.ImageUrls.Count);
        int imageIndex = 0;
        foreach (string imageLink in advertising.ImageUrls)
        {
            int currentImageIndex = imageIndex;
            imageDownloads.Add(DownloadImageIfMissingAsync(
                paths,
                ModificationType.Advertising,
                advertising.Name,
                currentImageIndex.ToString(CultureInfo.CurrentCulture),
                imageLink,
                cancellationToken));
            imageIndex++;
        }

        await Task.WhenAll(imageDownloads).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes the cached files of versions the catalog no longer lists and the user no longer holds.
    /// </summary>
    /// <remarks>
    ///     A card keeps every remote version it ever merged, so its version list alone cannot decide ownership. Files
    ///     are kept for the version the catalog lists, for each card's latest version because its tile shows that
    ///     version's art, and for versions the user holds locally, whose palette and shell artwork the catalog can no
    ///     longer supply.
    /// </remarks>
    public void RemoveRetiredModificationImages(
        LauncherContentVersion listedVersion,
        LauncherData data,
        LauncherPaths paths)
    {
        ArgumentNullException.ThrowIfNull(listedVersion);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(paths);

        var ownedBaseNames = ModificationImageCachePath.GetFolderOwners(data, listedVersion.Name)
            .SelectMany(content => content.Versions
                .Where(version => version.Installation.HasLocalState)
                .Append(content.LatestVersion))
            .Append(listedVersion)
            .SelectMany(ModificationImageCachePath.GetOwnedBaseNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        DeleteStaleImages(
            paths,
            listedVersion.ModificationType,
            listedVersion.Name,
            images => images.Where(image =>
                !ownedBaseNames.Contains(Path.GetFileNameWithoutExtension(image.Name))));
    }

    /// <summary>
    ///     Deletes the files <paramref name="selectStale" /> picks from one cached image folder.
    /// </summary>
    private void DeleteStaleImages(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName,
        Func<FileInfo[], IEnumerable<FileInfo>> selectStale)
    {
        try
        {
            string imageFolderPath = ModificationImageCachePath.ResolveDirectory(
                paths,
                modificationType,
                modificationName);
            if (!Directory.Exists(imageFolderPath))
            {
                return;
            }

            FileSystemPathSafety.EnsureDirectoryTreeHasNoReparsePoints(
                imageFolderPath,
                "Cached catalog image directories");
            foreach (FileInfo image in selectStale(new DirectoryInfo(imageFolderPath).GetFiles()))
            {
                try
                {
                    File.Delete(ModificationImageCachePath.ResolvePath(paths, image.FullName));
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Failed to delete stale cached image {ImageFileName} for {ModificationName}.",
                        image.Name,
                        modificationName);
                }
            }
        }
        catch (Exception exception) when (ModificationCacheFailure.IsRecoverable(exception))
        {
            _logger.LogWarning(
                exception,
                "Skipped stale image cleanup for {ModificationName} because its cache path was unavailable.",
                modificationName);
        }
    }

    private async Task DownloadImageIfMissingAsync(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName,
        string fileName,
        string link,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceUri = new Uri(link, UriKind.Absolute);
            string imageDirectory = OwnedDirectoryTree.EnsureExists(
                paths.ImagesDirectory,
                ModificationImageCachePath.ResolveDirectory(paths, modificationType, modificationName));
            FileSystemPathSafety.EnsureDirectoryTreeHasNoReparsePoints(
                imageDirectory,
                "Cached catalog image directories");
            string destinationFilePath = ModificationImageCachePath.ResolveRemoteImagePath(
                paths,
                modificationType,
                modificationName,
                fileName,
                sourceUri);
            await _assetDownloader.DownloadIfMissingAsync(
                sourceUri,
                destinationFilePath,
                cancellationToken).ConfigureAwait(false);
            FileSystemPathSafety.EnsureDirectoryTreeHasNoReparsePoints(
                imageDirectory,
                "Cached catalog image directories");
            _ = ModificationImageCachePath.ResolvePath(paths, destinationFilePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to download cached image {ImageName} for {ModificationName}.",
                fileName,
                modificationName);
        }
    }
}
