using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Archives;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Imports manually selected modification content by copying files and folders, extracting supported archives, and
///     converting <c>.big</c> packages to launcher-managed <c>.gib</c> files.
/// </summary>
/// <remarks>
///     Content is assembled in the manual-import staging folder and moved into the installed version folder only once
///     it is complete, through the same replacement downloads use. A failed or cancelled import therefore never leaves
///     a partial version behind for the catalog to list, and launch, as installed.
/// </remarks>
internal sealed class FileSystemManualModificationImporter : IManualModificationImporter
{
    /// <summary>
    ///     The number of nested single-folder wrappers unwrapped before the import is left as it arrived.
    /// </summary>
    private const int MaxWrapperDirectoryDepth = 4;

    /// <summary>
    ///     Folder names the game itself reads from a content root.
    /// </summary>
    /// <remarks>
    ///     An INI-only modification is often nothing but a <c>Data</c> folder. Unwrapping that would hoist its children
    ///     to the content root and destroy the very structure the game looks for, so a lone folder named here is
    ///     content rather than packaging.
    /// </remarks>
    private static readonly HashSet<string> _gameContentDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Data",
        "Art",
        "Maps",
        "Movies",
        "Music",
        "Window",
        "Lang",
        "Scripts",
        "Textures"
    };

    private readonly IArchiveExtractor _archiveExtractor;

    private readonly ILogger<FileSystemManualModificationImporter> _logger;

    public FileSystemManualModificationImporter(
        IArchiveExtractor archiveExtractor,
        ILogger<FileSystemManualModificationImporter> logger)
    {
        _archiveExtractor = archiveExtractor ?? throw new ArgumentNullException(nameof(archiveExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool Import(
        IReadOnlyList<string> sourcePaths,
        PackageUpdatePathSet paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(paths);

        if (sourcePaths.Count == 0)
        {
            throw new ArgumentException("At least one source file is required.", nameof(sourcePaths));
        }

        OwnedContentPath stagingPath = paths.TemporaryPath;
        string installedFolderName = Path.GetFileName(paths.InstalledPath.FullPath);
        try
        {
            OwnedDirectoryTree.PrepareEmpty(stagingPath.OwnerRoot, stagingPath.FullPath);
            foreach (string sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string stagingDirectory = PrepareSafeStaging(stagingPath);
                if (Directory.Exists(sourcePath))
                {
                    ImportDirectory(sourcePath, stagingDirectory, cancellationToken);
                    continue;
                }

                ImportFile(sourcePath, stagingDirectory, cancellationToken);
            }

            string contentRoot = FindContentRoot(PrepareSafeStaging(stagingPath));
            if (!Directory.EnumerateFiles(contentRoot, "*", FileSystemPathSafety.CreateRecursiveNoLinksOptions())
                    .Any())
            {
                _logger.LogWarning("Manual import found no files to install for {DestinationDirectory}.",
                    installedFolderName);
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            PackageInstallFolderReplacer.Replace(
                new OwnedContentPath(stagingPath.OwnerRoot, contentRoot),
                paths.InstalledPath,
                paths.BackupPath,
                _logger);

            _logger.LogInformation(
                "Imported {SourceCount} manual content source(s) to {DestinationDirectory}.",
                sourcePaths.Count,
                installedFolderName);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Failed to import manual content into {DestinationDirectory}.",
                installedFolderName);
            throw;
        }
        finally
        {
            DeleteStaging(stagingPath);
        }
    }

    private void ImportFile(
        string sourceFilePath,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        string sourceFileName = Path.GetFileName(sourceFilePath);
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source file path must include a file name.", nameof(sourceFilePath));
        }

        if (!LauncherContentFileTypes.IsArchive(sourceFileName))
        {
            CopyImportedFile(sourceFilePath, sourceFileName, stagingDirectory);
            return;
        }

        // Archives carry the same packages a download does, so their .big entries are stored under the inert .gib
        // name here for the same reason: an imported archive and a downloaded one must leave the same folder behind.
        // The archive is read where the user keeps it. A copy beside its extracted entries would cost a second full
        // write and collide with an archive that holds an entry of its own name.
        try
        {
            _archiveExtractor.ExtractToDirectory(
                sourceFilePath,
                stagingDirectory,
                true,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ManualImportArchiveException(sourceFileName, exception);
        }
    }

    /// <summary>
    ///     Copies the contents of a selected folder into the destination, preserving its subfolder structure.
    /// </summary>
    /// <remarks>
    ///     The folder itself is not recreated: a user selecting a folder is naming the content root, not asking for
    ///     another level around it. Archives inside a selected folder are copied rather than extracted, because the
    ///     folder is imported as the author arranged it.
    /// </remarks>
    private void ImportDirectory(
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        if (FileSystemPathSafety.IsReparsePoint(sourceDirectory))
        {
            _logger.LogWarning(
                "Skipped manual import source {DirectoryName} because it is a link.",
                Path.GetFileName(sourceDirectory));
            return;
        }

        var directory = new DirectoryInfo(sourceDirectory);
        foreach (FileInfo file in directory.GetFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                _logger.LogWarning("Skipped linked manual import file {FileName}.", file.Name);
                continue;
            }

            CopyImportedFile(file.FullName, file.Name, destinationDirectory);
        }

        foreach (DirectoryInfo childDirectory in directory.GetDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((childDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                _logger.LogWarning("Skipped linked manual import folder {DirectoryName}.", childDirectory.Name);
                continue;
            }

            string childDestination = ResolveSafeDestinationFilePath(
                destinationDirectory,
                Path.Combine(destinationDirectory, childDirectory.Name));
            FileSystemPathSafety.EnsureExistingPathChainHasNoReparsePoints(
                childDestination,
                "Manual import destinations");
            Directory.CreateDirectory(childDestination);
            ImportDirectory(childDirectory.FullName, childDestination, cancellationToken);
        }
    }

    /// <summary>
    ///     Copies one imported file under its installed name, leaving content that is already in place untouched.
    /// </summary>
    /// <remarks>
    ///     The copy is launcher-owned content, so it drops a read-only flag carried over from the source, such as a disc.
    ///     A kept flag would make every launch copy the file instead of hard-linking it, and would stop an archive in
    ///     the same import from extracting an entry of the same name over it.
    /// </remarks>
    private static void CopyImportedFile(
        string sourceFilePath,
        string fileName,
        string destinationDirectory)
    {
        string destinationFilePath = ResolveSafeDestinationFilePath(
            destinationDirectory,
            Path.Combine(destinationDirectory, fileName));
        string installedFilePath = BigFileVariantPath.GetInstalledPath(destinationFilePath);
        if (!LexicalPath.AreEquivalent(installedFilePath, destinationFilePath))
        {
            installedFilePath = ResolveSafeDestinationFilePath(destinationDirectory, installedFilePath);
        }

        if (!File.Exists(installedFilePath))
        {
            File.Copy(sourceFilePath, installedFilePath);
            FileSystemPathSafety.ClearReadOnly(installedFilePath);
        }
    }

    /// <summary>
    ///     Finds the staged folder that holds the content, skipping packaging folders that wrap it so an archive built
    ///     around its own folder does not install one level below where the game reads it.
    /// </summary>
    /// <remarks>
    ///     A lone folder is only ever packaging when nothing sits beside it. Any second entry at the content root means
    ///     the author placed that folder deliberately, and it is left alone. The innermost wrapper is installed as the
    ///     version folder rather than having its children moved up, so a wrapper holding a folder of its own name
    ///     cannot collide with itself.
    /// </remarks>
    private string FindContentRoot(string stagingDirectory)
    {
        string contentRoot = stagingDirectory;
        for (int depth = 0; depth < MaxWrapperDirectoryDepth; depth++)
        {
            string[] entries = Directory.GetFileSystemEntries(contentRoot);
            if (entries.Length != 1)
            {
                break;
            }

            string wrapperDirectory = entries[0];
            string wrapperName = Path.GetFileName(wrapperDirectory);
            if (!Directory.Exists(wrapperDirectory) ||
                FileSystemPathSafety.IsReparsePoint(wrapperDirectory) ||
                _gameContentDirectoryNames.Contains(wrapperName))
            {
                break;
            }

            _logger.LogInformation(
                "Unwrapped manual import packaging folder {DirectoryName}.",
                wrapperName);
            contentRoot = wrapperDirectory;
        }

        return contentRoot;
    }

    /// <summary>
    ///     Removes the staging folder after an import, whether it was installed, failed, or cancelled.
    /// </summary>
    /// <remarks>
    ///     A failure here never replaces the import's own outcome: startup empties Temp, so leftover staging is swept
    ///     on the next start.
    /// </remarks>
    private void DeleteStaging(OwnedContentPath stagingPath)
    {
        try
        {
            OwnedDirectoryTree.DeleteIfExists(stagingPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger.LogWarning(
                exception,
                "Could not clear the manual import staging folder; it is cleared on the next start.");
        }
    }

    /// <summary>
    ///     Creates the owned staging folder when needed and rejects any linked path before mutation or extraction.
    /// </summary>
    private static string PrepareSafeStaging(OwnedContentPath stagingPath)
    {
        string stagingDirectory = FileSystemPathSafety.ResolveOwnedSubpath(
            stagingPath.OwnerRoot,
            stagingPath.FullPath,
            "Manual import destinations",
            "their launcher-owned root");
        stagingDirectory = OwnedDirectoryTree.EnsureExists(
            stagingPath.OwnerRoot,
            stagingDirectory);
        FileSystemPathSafety.EnsureDirectoryTreeHasNoReparsePoints(
            stagingDirectory,
            "Manual import destinations");
        return stagingDirectory;
    }

    /// <summary>
    ///     Resolves one destination file and rejects paths or existing entries outside the safe import directory.
    /// </summary>
    private static string ResolveSafeDestinationFilePath(
        string destinationDirectory,
        string candidatePath)
    {
        return FileSystemPathSafety.ResolveOwnedSubpath(
            destinationDirectory,
            candidatePath,
            "Manual import files",
            "their destination directory");
    }
}
