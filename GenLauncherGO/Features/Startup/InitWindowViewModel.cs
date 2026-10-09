using System;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Shared.Localization;
using GenLauncherGO.Shared.Remote;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Coordinates launcher initialization before the main window opens.
/// </summary>
internal sealed class InitWindowViewModel
{
    private readonly ILauncherContentCatalog _catalog;
    private readonly IRemoteConnectionProbe _connectionProbe;
    private readonly GameFolderWriteAccess _gameFolderWriteAccess;

    private readonly ILaunchPreparationService _launchPreparationService;

    private readonly ILauncherPathResolver _launcherPathResolver;

    private readonly LauncherRuntimeContext _runtimeContext;

    private readonly IStartupDialogService _startupDialogService;

    private readonly ILauncherStringLocalizer _stringLocalizer;

    private bool _startupAborted;

    public InitWindowViewModel(
        IRemoteConnectionProbe connectionProbe,
        ILaunchPreparationService launchPreparationService,
        GameFolderWriteAccess gameFolderWriteAccess,
        ILauncherPathResolver launcherPathResolver,
        ILauncherContentCatalog catalog,
        LauncherRuntimeContext runtimeContext,
        ILauncherStringLocalizer stringLocalizer,
        IStartupDialogService startupDialogService)
    {
        _connectionProbe = connectionProbe ?? throw new ArgumentNullException(nameof(connectionProbe));
        _launchPreparationService = launchPreparationService ??
                                    throw new ArgumentNullException(nameof(launchPreparationService));
        _gameFolderWriteAccess = gameFolderWriteAccess ??
                                 throw new ArgumentNullException(nameof(gameFolderWriteAccess));
        _launcherPathResolver = launcherPathResolver ?? throw new ArgumentNullException(nameof(launcherPathResolver));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
        _startupDialogService = startupDialogService ?? throw new ArgumentNullException(nameof(startupDialogService));
    }

    /// <summary>
    ///     Occurs when launcher startup has completed and the main window can be shown.
    /// </summary>
    public event EventHandler<InitWindowStartupCompletedEventArgs>? StartupCompleted;

    /// <summary>
    ///     Occurs when startup cancellation requires application shutdown.
    /// </summary>
    public event EventHandler? ShutdownRequested;

    /// <summary>
    ///     Runs startup preparation and raises completion events for the owning view.
    /// </summary>
    /// <returns>A task that completes when startup has either completed or aborted.</returns>
    public async Task StartAsync()
    {
        await Task.Run(() => _launcherPathResolver.PrepareGameDirectories(
            _runtimeContext.LauncherPaths,
            true));

        bool connected = await PrepareLauncherAsync();
        if (_startupAborted)
        {
            return;
        }

        _runtimeContext.Connected = connected;
        StartupCompleted?.Invoke(this, new InitWindowStartupCompletedEventArgs(connected));
    }

    /// <summary>
    ///     Initializes launcher content, runtime state, and remote connection state.
    /// </summary>
    /// <returns><see langword="true" /> when remote services are available.</returns>
    public async Task<bool> PrepareLauncherAsync()
    {
        LauncherPaths paths = _runtimeContext.LauncherPaths;
        // Recovery writes to the game folder, so a folder the launcher cannot change must stop startup before it.
        if (!await RetryUntilSucceededAsync(
                () => _gameFolderWriteAccess.EnsureWritableAsync(paths, CancellationToken.None),
                _stringLocalizer["GameFolderAccessDenied"],
                GameFolderWriteAccess.DescribeAccessDenied(_stringLocalizer, paths)))
        {
            return false;
        }

        if (!await RetryUntilSucceededAsync(
                () => RecoverDeploymentAsync(paths),
                "GenLauncherGO",
                _stringLocalizer["DeploymentRecoveryFailed"]))
        {
            return false;
        }

        _runtimeContext.GameFolderRecovered = true;
        _runtimeContext.ApplyActiveGameDefaults();

        bool connected = await CheckConnectionAsync();

        await _catalog.InitDataAsync(
            CreateRemoteManifestUri(connected),
            _runtimeContext.LauncherPaths,
            CancellationToken.None);

        if (_catalog.RepositoryModificationNames == null)
        {
            connected = false;
        }

        if (connected)
        {
            LauncherContent? lastActivatedMod = _catalog.Data.GetSelectedMod();

            if (lastActivatedMod != null)
            {
                await _catalog.ReadPatchesAndAddonsForModAsync(
                    lastActivatedMod.ContentKey,
                    CancellationToken.None);
            }
        }
        else
        {
            await _startupDialogService.ShowMessageAsync(
                _stringLocalizer["Info"],
                _stringLocalizer["CannotConnect"]);
        }

        return connected;
    }

    /// <summary>
    ///     Repeats a startup step until it succeeds, or until the user cancels its Retry/Cancel warning and stops startup.
    /// </summary>
    /// <returns><see langword="false" /> when the user stopped startup.</returns>
    private async Task<bool> RetryUntilSucceededAsync(Func<Task<bool>> step, string title, string message)
    {
        while (!await step())
        {
            if (!await _startupDialogService.ShowRetryCancelWarningAsync(title, message))
            {
                _startupAborted = true;
                ShutdownRequested?.Invoke(this, EventArgs.Empty);
                return false;
            }
        }

        return true;
    }

    private Uri? CreateRemoteManifestUri(bool connected)
    {
        if (!connected || string.IsNullOrWhiteSpace(_runtimeContext.ModsRepositoryUrl))
        {
            return null;
        }

        return new Uri(_runtimeContext.ModsRepositoryUrl, UriKind.Absolute);
    }

    /// <summary>
    ///     Attempts to recover from incomplete launch deployment state.
    /// </summary>
    /// <returns><see langword="true" /> when recovery succeeded.</returns>
    private Task<bool> RecoverDeploymentAsync(LauncherPaths paths)
    {
        return Task.Run(() =>
        {
            return _launchPreparationService.Recover(
                paths,
                CancellationToken.None);
        });
    }

    /// <summary>
    ///     Checks whether the active remote manifest URL is reachable.
    /// </summary>
    /// <returns><see langword="true" /> when the remote URL is reachable.</returns>
    private async Task<bool> CheckConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(_runtimeContext.ModsRepositoryUrl))
        {
            return false;
        }

        return await _connectionProbe.CanConnectAsync(
            new Uri(_runtimeContext.ModsRepositoryUrl, UriKind.Absolute),
            CancellationToken.None);
    }
}
