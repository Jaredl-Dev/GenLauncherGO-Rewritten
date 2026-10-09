using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Launching;

/// <summary>
///     Ensures the launcher can change files in a game folder before deployment or recovery writes there.
/// </summary>
/// <remarks>
///     The launcher runs without administrator rights, so protected folders such as Program Files refuse its writes.
///     When they do, one elevated <c>icacls</c> run gives the current user Modify rights that the folder's files and
///     subfolders inherit. Store repairs can reset those rights, so callers check every time a game folder becomes
///     active.
/// </remarks>
internal sealed class GameFolderWriteAccess
{
    /// <summary>
    ///     The Win32 error ShellExecute reports when the user dismisses the UAC prompt.
    /// </summary>
    private const int ErrorCancelled = 1223;

    private readonly ILogger<GameFolderWriteAccess> _logger;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<int>> _runElevated;

    public GameFolderWriteAccess(ILogger<GameFolderWriteAccess> logger)
        : this(logger, RunElevatedAsync)
    {
    }

    internal GameFolderWriteAccess(
        ILogger<GameFolderWriteAccess> logger,
        Func<ProcessStartInfo, CancellationToken, Task<int>> runElevated)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runElevated = runElevated ?? throw new ArgumentNullException(nameof(runElevated));
    }

    /// <summary>
    ///     Confirms the launcher can create files in the game folder, asking Windows once to grant access when it cannot.
    /// </summary>
    /// <returns>
    ///     <see langword="false" /> when the user declines the prompt, the grant fails, or the folder still refuses new
    ///     files.
    /// </returns>
    public async Task<bool> EnsureWritableAsync(LauncherPaths paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (DirectoryWriteProbe.CanCreateFiles(paths.GameDirectory))
        {
            return true;
        }

        _logger.LogInformation("The {Game} folder refuses new files; requesting write access.", paths.Game);
        int exitCode;
        try
        {
            exitCode = await _runElevated(CreateGrant(paths.GameDirectory), cancellationToken);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            _logger.LogInformation("The write access request for the {Game} folder was declined.", paths.Game);
            return false;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogError(exception, "Could not request write access to the {Game} folder.", paths.Game);
            return false;
        }

        if (exitCode != 0)
        {
            _logger.LogError(
                "Granting write access to the {Game} folder failed with exit code {ExitCode}.",
                paths.Game,
                exitCode);
            return false;
        }

        if (!DirectoryWriteProbe.CanCreateFiles(paths.GameDirectory))
        {
            _logger.LogWarning("The {Game} folder still refuses new files after write access was granted.", paths.Game);
            return false;
        }

        _logger.LogInformation("Granted write access to the {Game} folder.", paths.Game);
        return true;
    }

    /// <summary>
    ///     Describes the elevated grant. Call on the UI thread, which owns the window the prompt is parented to.
    /// </summary>
    private static ProcessStartInfo CreateGrant(string gameDirectory)
    {
        // Read unelevated: when a standard user approves with administrator credentials, icacls runs as that
        // administrator, but the access belongs to the user running the launcher.
        using var identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier user = identity.User ??
                                  throw new InvalidOperationException(
                                      "The current Windows user has no security identifier.");
        Window? owner = AvaloniaDialog.ResolveApplicationOwner(null);
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "icacls.exe"))
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            // .NET hands ShellExecuteEx a parent window only alongside ErrorDialog. Without one, Windows shows the
            // consent prompt behind the launcher as a flashing taskbar button.
            ErrorDialog = true,
            ErrorDialogParentHandle = owner?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero
        };
        startInfo.ArgumentList.Add(gameDirectory);
        startInfo.ArgumentList.Add("/grant");
        // Object and container inheritance carry the grant to every file and subfolder that inherits from the folder.
        startInfo.ArgumentList.Add($"*{user.Value}:(OI)(CI)M");
        return startInfo;
    }

    [ExcludeFromCodeCoverage(Justification =
        "Starts an elevated system process; the grant it runs is covered through the injected runner.")]
    private static async Task<int> RunElevatedAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        // ShellExecuteEx returns only after the user answers the consent prompt, so keep it off the UI thread.
        using Process process = await Task.Run(() => Process.Start(startInfo), cancellationToken) ??
                                throw new InvalidOperationException("Windows did not start the write access grant.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
