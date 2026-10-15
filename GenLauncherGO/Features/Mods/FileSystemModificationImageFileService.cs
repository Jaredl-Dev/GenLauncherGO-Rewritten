using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Manages cached modification image files on disk.
/// </summary>
internal sealed class FileSystemModificationImageFileService : IModificationImageFileService
{
    private readonly ILogger<FileSystemModificationImageFileService> _logger;
    private readonly LauncherRuntimePathContext _runtimePathContext;

    public FileSystemModificationImageFileService(
        LauncherRuntimePathContext runtimePathContext,
        ILogger<FileSystemModificationImageFileService> logger)
    {
        _runtimePathContext = runtimePathContext ?? throw new ArgumentNullException(nameof(runtimePathContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? FindExistingImageFilePath(
        ModificationType modificationType,
        string modificationName,
        string imageBaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modificationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageBaseName);

        return GetImageFiles(
                _runtimePathContext.ActivePaths,
                modificationType,
                modificationName,
                imageBaseName)
            .FirstOrDefault();
    }

    public int CountImageFiles(ModificationType modificationType, string modificationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modificationName);

        LauncherPaths paths = _runtimePathContext.ActivePaths;
        string? imageDirectory = ModificationImageCachePath.ResolveExistingDirectory(
            paths,
            modificationType,
            modificationName);
        if (imageDirectory is null)
        {
            return 0;
        }

        return Directory.EnumerateFiles(imageDirectory).Count();
    }

    public bool TryDeleteImage(
        ModificationType modificationType,
        string modificationName,
        string imageBaseName)
    {
        try
        {
            foreach (string imageFilePath in GetImageFiles(
                             _runtimePathContext.ActivePaths,
                             modificationType,
                             modificationName,
                             imageBaseName))
            {
                File.Delete(imageFilePath);
            }

            return true;
        }
        catch (Exception exception) when (ModificationCacheFailure.IsRecoverable(exception))
        {
            _logger.LogWarning(
                exception,
                "Could not remove cached modification image {ImageBaseName} for {ModificationName}.",
                imageBaseName,
                modificationName);
            return false;
        }
    }

    public Task<string> ReplaceImageAsync(
        string modificationName,
        string imageBaseName,
        string sourceImagePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modificationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageBaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);

        LauncherPaths paths = _runtimePathContext.ActivePaths;
        return Task.Run(
            () => ReplaceImage(paths, modificationName, imageBaseName, sourceImagePath, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    ///     Replaces the cached image file and removes stale sibling extensions.
    /// </summary>
    private string ReplaceImage(
        LauncherPaths paths,
        string modificationName,
        string imageBaseName,
        string sourceImagePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string extension = Path.GetExtension(sourceImagePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException(
                "The source image must have a file extension.",
                nameof(sourceImagePath));
        }

        string sourcePath = LexicalPath.NormalizeFullPath(sourceImagePath);

        try
        {
            ModificationImageCachePath.EnsureDirectory(paths, ModificationType.Mod, modificationName);
            string destinationPath = ModificationImageCachePath.ResolveImagePath(
                paths,
                ModificationType.Mod,
                modificationName,
                imageBaseName + extension);

            if (LexicalPath.AreEquivalent(sourcePath, destinationPath))
            {
                return destinationPath;
            }

            foreach (string existingImagePath in GetImageFiles(
                         paths,
                         ModificationType.Mod,
                         modificationName,
                         imageBaseName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(existingImagePath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(sourcePath, destinationPath);
            return destinationPath;
        }
        catch (Exception exception) when (ModificationCacheFailure.IsRecoverable(exception))
        {
            _logger.LogError(
                exception,
                "Could not replace cached modification image {ImageBaseName} for {ModificationName}.",
                imageBaseName,
                modificationName);
            throw new IOException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Could not replace cached image '{0}' for modification '{1}'.",
                    imageBaseName,
                    modificationName),
                exception);
        }
    }

    /// <summary>
    ///     Lists the owned cached files stored under one base name, whatever their extension, or none when the
    ///     modification has no cache folder.
    /// </summary>
    /// <remarks>
    ///     Compares whole base names because a <c>name.*</c> search pattern cannot isolate one version: a tile's base
    ///     name is the bare version, so <c>1.0.*</c> also matches every cached file of version 1.0.1. Returns a
    ///     snapshot, so callers can delete the files it lists.
    /// </remarks>
    private static IReadOnlyList<string> GetImageFiles(
        LauncherPaths paths,
        ModificationType modificationType,
        string modificationName,
        string imageBaseName)
    {
        string? imageDirectory = ModificationImageCachePath.ResolveExistingDirectory(
            paths,
            modificationType,
            modificationName);
        if (imageDirectory is null)
        {
            return [];
        }

        string validatedImagePath = ModificationImageCachePath.ResolveImagePath(
            paths,
            modificationType,
            modificationName,
            imageBaseName + ".cache");
        string validatedBaseName = Path.GetFileNameWithoutExtension(validatedImagePath);
        return Directory.EnumerateFiles(imageDirectory)
            .Where(imageFilePath => string.Equals(
                Path.GetFileNameWithoutExtension(imageFilePath),
                validatedBaseName,
                StringComparison.OrdinalIgnoreCase))
            .Select(imageFilePath => ModificationImageCachePath.ResolvePath(paths, imageFilePath))
            .ToList();
    }
}
