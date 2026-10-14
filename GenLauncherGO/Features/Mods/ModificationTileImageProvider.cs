using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GenLauncherGO.Features.Startup;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Loads Avalonia image sources for one modification tile.
/// </summary>
internal sealed class ModificationTileImageProvider
{
    private readonly ModificationImageSourceFactory _imageSourceFactory;

    private readonly LauncherRuntimeContext _launcherContext;

    private readonly ILogger _logger;

    private readonly IModificationImageFileService _modificationImageFileService;

    private int _advertisingImageIndex = -1;

    public ModificationTileImageProvider(
        ModificationImageSourceFactory imageSourceFactory,
        LauncherRuntimeContext launcherContext,
        IModificationImageFileService modificationImageFileService,
        ILogger logger)
    {
        _imageSourceFactory = imageSourceFactory ?? throw new ArgumentNullException(nameof(imageSourceFactory));
        _launcherContext = launcherContext ?? throw new ArgumentNullException(nameof(launcherContext));
        _modificationImageFileService = modificationImageFileService ??
                                        throw new ArgumentNullException(nameof(modificationImageFileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Loads the cached shell artwork a version published with its palette, if it published one and it downloaded.
    /// </summary>
    /// <remarks>
    ///     Returns <see langword="null" /> when the modification declared no artwork or the download has not landed yet,
    ///     which the caller treats as "keep the active game's artwork" rather than as a failure to theme.
    /// </remarks>
    public IImageBrush? LoadThemeBackground(LauncherContent modification, LauncherContentVersion version)
    {
        ArgumentNullException.ThrowIfNull(modification);
        ArgumentNullException.ThrowIfNull(version);

        if (version.Theme?.HasBackgroundImage != true)
        {
            return null;
        }

        string? imagePath = _modificationImageFileService.FindExistingImageFilePath(
            modification.ModificationType,
            modification.Name,
            LauncherContentTheme.ResolveBackgroundImageBaseName(version.Version));
        Bitmap? background = _imageSourceFactory.LoadFileImage(imagePath, false);
        if (background is null)
        {
            return null;
        }

        return (IImageBrush)new ImageBrush(background)
        {
            Stretch = Stretch.Fill
        }.ToImmutable();
    }

    /// <summary>
    ///     Loads the grayscale image for a modification tile.
    /// </summary>
    public IImage? LoadGrayscaleImage(
        LauncherContent modification,
        LauncherContentVersion latestVersion,
        bool localMod)
    {
        if (modification.ModificationType == ModificationType.Mod)
        {
            return LoadModImage(modification, latestVersion, localMod, grayscale: true);
        }

        if (modification.ModificationType == ModificationType.Advertising)
        {
            return LoadAdvertisingImage(modification);
        }

        return null;
    }

    /// <summary>
    ///     Loads the color image for a selected modification tile.
    /// </summary>
    public IImage? LoadColorImage(
        LauncherContent modification,
        LauncherContentVersion latestVersion,
        bool localMod)
    {
        if (modification.ModificationType != ModificationType.Mod)
        {
            return null;
        }

        return LoadModImage(modification, latestVersion, localMod, grayscale: false);
    }

    private IImage? LoadAdvertisingImage(LauncherContent modification)
    {
        int filesCount = _modificationImageFileService.CountImageFiles(
            modification.ModificationType,
            modification.Name);
        if (filesCount <= 0)
        {
            return null;
        }

        if (_advertisingImageIndex == -1)
        {
            var random = new Random();
            int value = random.Next(0, 30);
            _advertisingImageIndex = value == 0
                ? random.Next(filesCount / 2, filesCount)
                : random.Next(0, filesCount / 2);
        }

        return LoadImage(
            modification.ModificationType,
            modification.Name,
            _advertisingImageIndex.ToString(CultureInfo.InvariantCulture),
            false);
    }

    /// <summary>
    ///     Loads a mod tile's cached cover image, or the game's fallback banner when there is none it can show.
    /// </summary>
    private IImage? LoadModImage(
        LauncherContent modification,
        LauncherContentVersion latestVersion,
        bool localMod,
        bool grayscale)
    {
        Bitmap? image = LoadImage(
            modification.ModificationType,
            modification.Name,
            latestVersion.Version,
            grayscale);
        return image ?? _imageSourceFactory.LoadDefaultImage(_launcherContext.CurrentlyManagedGame, localMod, grayscale);
    }

    /// <summary>
    ///     Loads a cached image, or returns <see langword="null" /> when it is missing or cannot be read.
    /// </summary>
    /// <remarks>
    ///     Tile art and advertising share this loader and its failure policy: an image that cannot be decoded is deleted,
    ///     published artwork then counts as missing, and the catalog downloads it again.
    /// </remarks>
    private Bitmap? LoadImage(
        ModificationType modificationType,
        string modificationName,
        string imageBaseName,
        bool grayscale)
    {
        try
        {
            return _imageSourceFactory.LoadFileImage(
                _modificationImageFileService.FindExistingImageFilePath(
                    modificationType,
                    modificationName,
                    imageBaseName),
                grayscale);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not load cached image {ImageBaseName} for {ModificationName}; attempting to remove it.",
                imageBaseName,
                modificationName);

            _modificationImageFileService.TryDeleteImage(modificationType, modificationName, imageBaseName);
            return null;
        }
    }
}
