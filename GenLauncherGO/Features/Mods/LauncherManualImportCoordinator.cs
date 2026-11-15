using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using GenLauncherGO.Features.Integrity;
using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.IO;
using GenLauncherGO.Shared.Localization;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Coordinates user-selected manual content imports with dialogs, catalog refresh, and integrity snapshots.
/// </summary>
internal sealed class LauncherManualImportCoordinator
{
    private const string NameUsedInParentKey = "NameUsedInMod";

    private readonly ILauncherContentCatalog _catalog;

    private readonly ILauncherDialogService _dialogService;
    private readonly ILauncherFilePicker _filePicker;

    private readonly LaunchContentIntegrityCoordinator _integrityCoordinator;

    private readonly ILogger<LauncherManualImportCoordinator> _logger;

    private readonly IManualModificationImporter _manualModificationImporter;

    private readonly LauncherRuntimePathContext _runtimePaths;

    private readonly ILauncherStringLocalizer _stringLocalizer;

    public LauncherManualImportCoordinator(
        ILauncherFilePicker filePicker,
        ILauncherDialogService dialogService,
        ILauncherContentCatalog catalog,
        LauncherRuntimePathContext runtimePaths,
        IManualModificationImporter manualModificationImporter,
        LaunchContentIntegrityCoordinator integrityCoordinator,
        ILauncherStringLocalizer stringLocalizer,
        ILogger<LauncherManualImportCoordinator> logger)
    {
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _runtimePaths = runtimePaths ?? throw new ArgumentNullException(nameof(runtimePaths));
        _manualModificationImporter = manualModificationImporter ??
                                      throw new ArgumentNullException(nameof(manualModificationImporter));
        _integrityCoordinator = integrityCoordinator ?? throw new ArgumentNullException(nameof(integrityCoordinator));
        _stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Runs the manual import workflow and returns the catalog entry that should be shown in the UI.
    /// </summary>
    /// <param name="kind">The kind of content to import.</param>
    /// <param name="owner">The window that owns the import dialogs.</param>
    /// <param name="parentContentName">The parent modification name, or <see langword="null" /> for original-game content.</param>
    /// <param name="cancellationToken">The token used to cancel import work.</param>
    /// <returns>The imported catalog entry, or <see langword="null" /> when the user cancels.</returns>
    public async Task<LauncherContent?> ImportAsync(
        ModificationType kind,
        Window owner,
        string? parentContentName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        LauncherPaths launcherPaths = _runtimePaths.ActivePaths;

        IReadOnlyList<string> selectedFiles = await PickImportSourcesAsync(launcherPaths, owner);
        if (selectedFiles.Count == 0)
        {
            return null;
        }

        string resolvedParentContentName = GetParentContentName(kind, parentContentName);
        ManualModificationDialogResult? importResult = await _dialogService.ShowManualModificationImportAsync(
            selectedFiles,
            name => GetNameRefusal(kind, resolvedParentContentName, name),
            owner);

        if (importResult == null)
        {
            return null;
        }

        LauncherContentKey importedKey = CreateContentKey(
            kind,
            resolvedParentContentName,
            importResult.ModificationName,
            importResult.Version);
        var importPaths = PackageUpdatePathSet.CreateForManualImport(
            launcherPaths,
            ResolveDestinationPath(launcherPaths, importedKey));

        bool installed;
        try
        {
            installed = await Task.Run(
                () => _manualModificationImporter.Import(
                    selectedFiles,
                    importPaths,
                    cancellationToken),
                cancellationToken);
        }
        catch (ManualImportArchiveException exception)
        {
            await ShowImportErrorAsync(
                string.Format(
                    CultureInfo.CurrentCulture,
                    _stringLocalizer["ManualImportArchiveUnreadable"],
                    exception.ArchiveFileName,
                    exception.InnerException?.Message),
                owner);
            return null;
        }

        if (!installed)
        {
            await ShowImportErrorAsync(_stringLocalizer["ManualImportNothingToImport"], owner);
            return null;
        }

        _catalog.UpdateLocalModificationsData();

        LauncherContent savedModification = _catalog.Data.FindContent(importedKey)
                                            ?? throw CreateMissingImportedModificationException(kind,
                                                importResult.ModificationName);

        LauncherContentVersion savedVersion = savedModification.Versions
                                                  .FirstOrDefault(version => version.ContentKey == importedKey)
                                              ?? throw CreateMissingImportedVersionException(
                                                  kind,
                                                  importResult.ModificationName,
                                                  importResult.Version);

        await _integrityCoordinator.RegisterManualImportAsync(savedVersion);

        return savedModification;
    }

    /// <summary>
    ///     Asks whether the import comes from files or a folder, then runs the matching picker.
    /// </summary>
    /// <remarks>
    ///     Windows offers no picker that selects files and folders together, so the choice has to be made before a
    ///     picker opens. Escape or closing the prompt cancels the import rather than picking either answer.
    /// </remarks>
    private async Task<IReadOnlyList<string>> PickImportSourcesAsync(LauncherPaths launcherPaths, Window owner)
    {
        bool? importFolder = await _dialogService.ShowInfoChoiceAsync(
            new LauncherInfoDialogRequest(
                _stringLocalizer["ManualImportSourceTitle"],
                _stringLocalizer["ManualImportSourceDetails"]),
            primaryActionText: _stringLocalizer["ManualImportSourceFolder"],
            secondaryActionText: _stringLocalizer["ManualImportSourceFiles"],
            owner: owner);
        if (importFolder == null)
        {
            return [];
        }

        if (importFolder == false)
        {
            return await _filePicker.PickManualPackageFilesAsync(owner);
        }

        string? selectedFolder = await _filePicker.PickManualContentFolderAsync(owner);
        if (selectedFolder == null)
        {
            return [];
        }

        // A folder holding the launcher's data would copy staging into itself, nesting one level deeper on every pass
        // until the process overflows its stack. A folder inside that data, such as Mods, would copy installed content
        // and launcher state into a new mod. Both folders are compared where Windows resolves them, because a drive
        // alias or a junction above the selected folder hides the overlap from the path text.
        if (LexicalPath.AreOverlapping(
                PhysicalDirectoryPath.ResolveExisting(launcherPaths.OwnedGameDataDirectory),
                PhysicalDirectoryPath.ResolveExisting(selectedFolder)))
        {
            _logger.LogWarning("Refused a manual import folder that holds the launcher's own data.");
            await ShowImportErrorAsync(_stringLocalizer["ManualImportFolderContainsLauncherData"], owner);
            return [];
        }

        if (Directory.Exists(Path.Combine(selectedFolder, LauncherFileSystemLayout.AddonsFolderName)) ||
            Directory.Exists(Path.Combine(selectedFolder, LauncherFileSystemLayout.PatchesFolderName)))
        {
            _logger.LogWarning("Refused a manual import folder that holds its own patches or add-ons.");
            await ShowImportErrorAsync(
                string.Format(
                    CultureInfo.CurrentCulture,
                    _stringLocalizer["ManualImportFolderHoldsAddonsOrPatches"],
                    launcherPaths.ModsDirectory),
                owner);
            return [];
        }

        return [selectedFolder];
    }

    /// <summary>
    ///     Explains why an import stopped, under the title the import details dialog uses for its own refusals.
    /// </summary>
    private Task ShowImportErrorAsync(string detail, Window owner)
    {
        return _dialogService.ShowErrorAsync(
            new LauncherInfoDialogRequest(_stringLocalizer["OperationAborted"], detail),
            owner);
    }

    /// <summary>
    ///     Returns the localization key explaining why other content already owns a name, or <see langword="null" />
    ///     when the import may use it.
    /// </summary>
    /// <remarks>
    ///     The original game's name is reserved for every kind: a mod with that name shares the original game's folder,
    ///     so deleting the mod would delete the game's patches and add-ons. A patch or add-on also may not repeat the name
    ///     of the mod or game it belongs to, or of another of that parent's patches and add-ons, except the same patch
    ///     or add-on receiving a new version. Different mods may reuse each other's names.
    /// </remarks>
    private string? GetNameRefusal(ModificationType kind, string parentContentName, string name)
    {
        if (LauncherContentKey.ForModificationName(name) == LauncherContentKey.OriginalGame)
        {
            return ManualAddModificationViewModel.ReservedOrInvalidNameKey;
        }

        if (kind == ModificationType.Mod)
        {
            return null;
        }

        var parentKey = LauncherContentKey.ForModificationName(parentContentName);
        bool nameUsedInParent = parentKey.HasName(name) ||
                                _catalog.Data.Patches.Concat(_catalog.Data.Addons).Any(content =>
                                    content.ContentKey.IsChildOf(parentKey) &&
                                    content.ContentKey.HasName(name) &&
                                    content.ModificationType != kind);
        return nameUsedInParent ? NameUsedInParentKey : null;
    }

    private static string GetParentContentName(
        ModificationType kind,
        string? parentContentName)
    {
        if (kind == ModificationType.Mod)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(parentContentName)
            ? LauncherContentKey.OriginalGame.Name
            : parentContentName;
    }

    /// <summary>
    ///     Resolves the canonical owned destination for imported package files.
    /// </summary>
    /// <param name="launcherPaths">The immutable active path snapshot for the import.</param>
    /// <param name="contentKey">The validated identity shared by path resolution and catalog lookup.</param>
    /// <returns>The validated launcher-owned destination path.</returns>
    private static OwnedContentPath ResolveDestinationPath(
        LauncherPaths launcherPaths,
        LauncherContentKey contentKey)
    {
        return LauncherContentPathResolver.ResolveVersionPath(
                   launcherPaths,
                   contentKey)
               ?? throw new InvalidOperationException("Manual content identity did not resolve to an owned path.");
    }

    private static LauncherContentKey CreateContentKey(
        ModificationType kind,
        string parentContentName,
        string modificationName,
        string version)
    {
        ModificationType modificationType = kind switch
        {
            ModificationType.Mod or ModificationType.Patch or ModificationType.Addon => kind,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown manual import kind.")
        };

        return new LauncherContentKey(
            modificationType,
            modificationType == ModificationType.Mod ? string.Empty : parentContentName,
            modificationName,
            version);
    }

    private InvalidOperationException CreateMissingImportedModificationException(
        ModificationType kind,
        string modificationName)
    {
        string importKindName = GetImportKindName(kind);
        _logger.LogError(
            "Manual import for {ImportKind} completed, but catalog entry {ModificationName} was not found after refresh.",
            importKindName,
            modificationName);

        return new InvalidOperationException(
            $"Imported {importKindName} '{modificationName}' was not found in the refreshed launcher catalog.");
    }

    private InvalidOperationException CreateMissingImportedVersionException(
        ModificationType kind,
        string modificationName,
        string version)
    {
        string importKindName = GetImportKindName(kind);
        _logger.LogError(
            "Manual import for {ImportKind} completed, but catalog entry {ModificationName} version {Version} was not found after refresh.",
            importKindName,
            modificationName,
            version);

        return new InvalidOperationException(
            $"Imported {importKindName} '{modificationName}' version '{version}' was not found in the refreshed launcher catalog.");
    }

    private static string GetImportKindName(ModificationType kind)
    {
        return kind == ModificationType.Mod ? "Modification" : kind.ToString();
    }
}
