using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Localization;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Launcher;

/// <summary>
///     Refreshes the lists in place when content was added to or removed from the Mods folder outside the launcher.
/// </summary>
/// <remarks>
///     The lists are built from the catalog, which is matched to the Mods folder when it loads and re-synced after each
///     launcher operation. Folders another program copies in or deletes stay out of the lists: either the folder differs
///     from the catalog, or an operation's re-sync recorded them in the catalog without rebuilding the lists.
/// </remarks>
internal sealed class LauncherModsFolderChangeCoordinator
{
    /// <summary>
    ///     How long the folder's version list must stay the same before the refresh, so a copy that is still adding
    ///     folders does not trigger one refresh per folder.
    /// </summary>
    private static readonly TimeSpan _settleDelay = TimeSpan.FromSeconds(2);

    private readonly ILauncherContentCatalog _catalog;
    private readonly LauncherCloseGuard _closeGuard;
    private readonly ILocalLauncherContentService _localContentService;
    private readonly ILogger<LauncherModsFolderChangeCoordinator> _logger;
    private readonly LauncherPackageActivityService _packageActivityService;
    private readonly LauncherRuntimePathContext _runtimePaths;
    private readonly ILauncherStringLocalizer _stringLocalizer;
    private CancellationTokenSource? _checkCancellation;
    private bool _checkInProgress;

    /// <summary>
    ///     Whether focus has come from another program since the last check.
    /// </summary>
    /// <remarks>
    ///     It starts set because the main window can open without focus, as a restarted launcher does when Windows keeps
    ///     the foreground with the program underneath. Its first activation then comes from another program with no
    ///     deactivation before it.
    /// </remarks>
    private bool _checkPending = true;

    private LauncherWindowContext? _context;
    private CancellationToken _windowLifetime;

    public LauncherModsFolderChangeCoordinator(
        ILauncherContentCatalog catalog,
        ILocalLauncherContentService localContentService,
        LauncherRuntimePathContext runtimePaths,
        LauncherCloseGuard closeGuard,
        LauncherPackageActivityService packageActivityService,
        ILauncherStringLocalizer stringLocalizer,
        ILogger<LauncherModsFolderChangeCoordinator> logger)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _localContentService = localContentService ?? throw new ArgumentNullException(nameof(localContentService));
        _runtimePaths = runtimePaths ?? throw new ArgumentNullException(nameof(runtimePaths));
        _closeGuard = closeGuard ?? throw new ArgumentNullException(nameof(closeGuard));
        _packageActivityService = packageActivityService ??
                                  throw new ArgumentNullException(nameof(packageActivityService));
        _stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _closeGuard.ActiveOperationsChanged += CloseGuard_ActiveOperationsChanged;
    }

    /// <summary>
    ///     Stops a check that is waiting, and marks the Mods folder for a check when focus leaves the launcher.
    /// </summary>
    public void HandleLauncherDeactivated(LauncherWindowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _checkCancellation?.Cancel();

        // A launcher dialog joins its owner's windows before it takes focus, so focus that leaves while none is open
        // went to another program, where the user may have changed the folder. Closing a launcher dialog then finds no
        // check pending.
        if (context.Owner.OwnedWindows.Count == 0)
        {
            _checkPending = true;
        }
    }

    /// <summary>
    ///     Checks the Mods folder when the launcher regains focus from another program.
    /// </summary>
    public Task HandleLauncherActivatedAsync(LauncherWindowContext context, CancellationToken windowLifetime)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _windowLifetime = windowLifetime;
        return CheckIfPendingAsync(context);
    }

    private void CloseGuard_ActiveOperationsChanged(object? sender, EventArgs eventArgs)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            HandleActiveOperationsChanged();
            return;
        }

        Dispatcher.UIThread.Post(HandleActiveOperationsChanged);
    }

    private void HandleActiveOperationsChanged()
    {
        if (_closeGuard.HasRunningOperations)
        {
            _checkCancellation?.Cancel();
            return;
        }

        if (_context is { } context)
        {
            _ = RetryCheckAfterActivityAsync(context);
        }
    }

    private async Task RetryCheckAfterActivityAsync(LauncherWindowContext context)
    {
        try
        {
            await CheckIfPendingAsync(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not check the Mods folder after launcher activity finished.");
        }
    }

    private bool CanCheck(Window owner)
    {
        return !_windowLifetime.IsCancellationRequested &&
               owner.IsActive &&
               owner.OwnedWindows.Count == 0 &&
               !_closeGuard.HasRunningOperations;
    }

    /// <summary>
    ///     Determines whether another workflow is changing the catalog or rebuilding the lists.
    /// </summary>
    /// <remarks>
    ///     Workflows that do so disable the main controls first, and some hold no package activity lease: a game
    ///     switch reloads the whole catalog, and selecting content loads its patches and add-ons. The catalog fills
    ///     off the UI thread meanwhile, so the folder is neither compared with it nor refreshed until they finish.
    /// </remarks>
    private static bool AreListsBusy(LauncherWindowContext context)
    {
        return !context.ViewModel.MainControlsEnabled;
    }

    private async Task CheckIfPendingAsync(LauncherWindowContext context)
    {
        if (_checkInProgress)
        {
            return;
        }

        _checkInProgress = true;
        try
        {
            // Focus can leave and come back while a check is still finishing. Leaving cancels that check and marks
            // the folder pending again, but the return found a check in progress and left, so the pending check
            // runs here instead.
            while (_checkPending && CanCheck(context.Owner))
            {
                _checkPending = false;
                try
                {
                    if (await WaitForSettledChangeAsync(context))
                    {
                        await RefreshListsAsync(context);
                    }
                }
                catch (OperationCanceledException) when (_windowLifetime.IsCancellationRequested)
                {
                    // The window is closing, and its lists with it.
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                      InvalidDataException)
                {
                    _logger.LogWarning(exception, "Could not compare the Mods folder with the launcher's lists.");
                }
            }
        }
        finally
        {
            _checkInProgress = false;
        }
    }

    /// <summary>
    ///     Samples the Mods folder until its version list stops changing and no copy is still writing a new version,
    ///     and reports whether the lists then miss a change.
    /// </summary>
    /// <remarks>
    ///     Sampling continues while the lists are busy, so the comparison waits for the workflow to finish rather than
    ///     needing another focus change. Each sample reads the active paths, so after a game switch the new game's
    ///     folder is compared with its own catalog.
    /// </remarks>
    /// <returns>
    ///     <see langword="false" /> when the lists are current, or when focus left or an activity started first.
    /// </returns>
    private async Task<bool> WaitForSettledChangeAsync(LauncherWindowContext context)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime);
        _checkCancellation = cancellation;
        try
        {
            HashSet<LauncherContentKey>? previousFolderVersions = null;
            while (true)
            {
                LauncherPaths paths = _runtimePaths.ActivePaths;
                HashSet<LauncherContentKey> folderVersions = await Task.Run(
                    () => _localContentService.FindInstalledVersions(paths)
                        .Select(version => version.ContentKey)
                        .ToHashSet(),
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();

                if (!AreListsBusy(context))
                {
                    HashSet<LauncherContentKey> listedVersions = _catalog.Data.GetInstalledVersionKeys();
                    IReadOnlySet<LauncherContentKey> unlistedChanges = _catalog.UnlistedFolderChanges;
                    if (unlistedChanges.Count == 0 && folderVersions.SetEquals(listedVersions))
                    {
                        return false;
                    }

                    if (previousFolderVersions?.SetEquals(folderVersions) == true)
                    {
                        // Besides versions new in the folder, the lists miss those an operation's re-sync recorded.
                        var unshownVersions = folderVersions
                            .Where(version => !listedVersions.Contains(version) || unlistedChanges.Contains(version))
                            .ToList();
                        if (!await IsCopyInProgressAsync(paths, unshownVersions, cancellation.Token))
                        {
                            _logger.LogInformation(
                                "The Mods folder changed outside the launcher. Versions added: {AddedCount}; " +
                                "versions removed: {RemovedCount}; changes recorded by an earlier re-sync: " +
                                "{UnlistedChangeCount}.",
                                folderVersions.Count(version => !listedVersions.Contains(version)),
                                listedVersions.Count(version => !folderVersions.Contains(version)),
                                unlistedChanges.Count);
                            return true;
                        }
                    }
                }

                previousFolderVersions = folderVersions;
                await Task.Delay(_settleDelay, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested &&
                                                 !_windowLifetime.IsCancellationRequested)
        {
            // Focus left or an activity started before the folder settled. Check again once the launcher is idle and
            // focused, so a copy the user returned from mid-way is still noticed.
            _checkPending = true;
            return false;
        }
        finally
        {
            _checkCancellation = null;
        }
    }

    /// <summary>
    ///     Determines whether a program is still writing a version that the lists do not show yet.
    /// </summary>
    /// <remarks>
    ///     A version counts as installed once it holds one file, so a copy whose files are large keeps the version list
    ///     unchanged long before it finishes. Listing the version then would let it launch with only part of its files.
    ///     A file another program keeps open for writing therefore holds the refresh back until that program closes it.
    ///     A launcher operation's re-sync records such a version in the catalog too, so being in the catalog does not
    ///     make a version safe to list.
    /// </remarks>
    private async Task<bool> IsCopyInProgressAsync(
        LauncherPaths paths,
        IReadOnlyList<LauncherContentKey> unshownVersions,
        CancellationToken cancellationToken)
    {
        bool copyInProgress = await Task.Run(
            () => unshownVersions.Any(version => _localContentService.IsVersionBeingWritten(paths, version)),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return copyInProgress;
    }

    /// <summary>
    ///     Re-syncs the catalog with the Mods folder and rebuilds the lists from it.
    /// </summary>
    /// <remarks>
    ///     The package activity lease keeps downloads, imports, removals, and repository additions from changing the
    ///     folder or the catalog meanwhile. It is taken without asking: when another operation started first, the check
    ///     runs again after it. A paused download is held as it is rather than waited for or stopped, and the rebuild
    ///     keeps its tile, so it resumes where it left off. Mods the repository publishes get their repository card the
    ///     way an import does. Lists that became busy after the comparison are left to that workflow, and the check
    ///     samples again once it finishes.
    /// </remarks>
    private async Task RefreshListsAsync(LauncherWindowContext context)
    {
        if (AreListsBusy(context) ||
            !_packageActivityService.TryBeginBesidePausedDownload(
                _stringLocalizer["ModsFolderRefreshActivity"],
                out LauncherPackageActivityService.LauncherPackageActivityLease? activityLease))
        {
            _checkPending = true;
            return;
        }

        using (activityLease)
        {
            await context.ReloadListsAsync(async () =>
            {
                _catalog.RefreshLocalModificationsData();
                foreach (LauncherContent modification in _catalog.Data.Modifications.ToList())
                {
                    await _catalog.MatchRepositoryModificationAsync(modification, _windowLifetime);
                }

                return true;
            });
        }

        _logger.LogInformation("Refreshed the lists from the Mods folder.");
    }
}
