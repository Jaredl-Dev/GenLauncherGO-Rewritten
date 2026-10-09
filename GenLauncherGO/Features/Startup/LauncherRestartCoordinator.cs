using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using GenLauncherGO.Features.Launcher;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Records a safe restart request for the application host to execute after Avalonia shutdown and cleanup.
/// </summary>
internal sealed class LauncherRestartCoordinator
{
    /// <summary>
    ///     The shared active-operation close guard.
    /// </summary>
    private readonly LauncherCloseGuard _closeGuard;

    /// <summary>
    ///     The logger used for restart-request diagnostics.
    /// </summary>
    private readonly ILogger<LauncherRestartCoordinator> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LauncherRestartCoordinator" /> class.
    /// </summary>
    /// <param name="closeGuard">The shared active-operation close guard.</param>
    /// <param name="logger">The logger used for restart-request diagnostics.</param>
    public LauncherRestartCoordinator(
        LauncherCloseGuard closeGuard,
        ILogger<LauncherRestartCoordinator> logger)
    {
        _closeGuard = closeGuard ?? throw new ArgumentNullException(nameof(closeGuard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Gets the replacement path requested for application shutdown.
    /// </summary>
    public LauncherRestartKind RestartKind { get; private set; }

    /// <summary>
    ///     Records an application restart after applying the shared active-operation guard, then closes the main window
    ///     so shutdown can start the replacement.
    /// </summary>
    /// <param name="owner">The main window, which also owns any blocking safety message.</param>
    /// <param name="restartKind">The replacement path shutdown should start.</param>
    /// <returns><see langword="true" /> when the restart was accepted and the window asked to close.</returns>
    public async Task<bool> TryRestartAsync(
        Window owner,
        LauncherRestartKind restartKind = LauncherRestartKind.Normal)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (restartKind == LauncherRestartKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(restartKind));
        }

        if (!await _closeGuard.CanCloseAsync(owner, LauncherCloseReason.Restart))
        {
            return false;
        }

        RestartKind = restartKind;
        _logger.LogInformation("Closing the main window to restart the launcher with kind {RestartKind}.", restartKind);
        owner.Close();
        return true;
    }

    /// <summary>
    ///     Drops an accepted restart whose window stayed open, so a later ordinary exit does not restart.
    /// </summary>
    public void CancelRestart()
    {
        if (RestartKind == LauncherRestartKind.None)
        {
            return;
        }

        _logger.LogInformation("The launcher stayed open, so the {RestartKind} restart was canceled.", RestartKind);
        RestartKind = LauncherRestartKind.None;
    }
}
