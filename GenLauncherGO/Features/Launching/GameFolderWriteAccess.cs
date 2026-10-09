using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.IO;
using GenLauncherGO.Shared.Localization;
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
    internal const int ErrorCancelled = 1223;

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
    ///     Describes a game folder that <see cref="EnsureWritableAsync" /> could not make writable.
    /// </summary>
    public static string DescribeAccessDenied(ILauncherStringLocalizer stringLocalizer, LauncherPaths paths)
    {
        ArgumentNullException.ThrowIfNull(stringLocalizer);
        ArgumentNullException.ThrowIfNull(paths);

        return string.Format(
            CultureInfo.CurrentCulture,
            stringLocalizer["GameFolderAccessDeniedDescription"],
            paths.GameDirectory);
    }

    /// <summary>
    ///     Confirms the launcher can create files in the game folder, asking Windows once to grant access when it cannot.
    /// </summary>
    /// <returns>
    ///     <see langword="false" /> when the folder cannot be checked or is on write-protected media, the user declines
    ///     the prompt, the grant fails or was already made, or the folder still refuses new files.
    /// </returns>
    /// <exception cref="InvalidDataException">The folder is reached through a reparse point.</exception>
    public async Task<bool> EnsureWritableAsync(LauncherPaths paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);

        try
        {
            // The checks wait on the drive, which can stall while a slow or sleeping one responds.
            return await Task.Run(() => EnsureWritableCoreAsync(paths, cancellationToken), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Reported as a refused folder, so startup offers Retry the way it does when recovery fails.
            _logger.LogError(exception, "Could not check write access to the {Game} folder.", paths.Game);
            return false;
        }
    }

    private async Task<bool> EnsureWritableCoreAsync(LauncherPaths paths, CancellationToken cancellationToken)
    {
        // Installation validation rejects reparse points, so this catches one swapped in since. The launcher never
        // writes through one, and an elevated grant must not follow one to another folder.
        FileSystemPathSafety.EnsureExistingPathChainHasNoReparsePoints(paths.GameDirectory, "Game installation paths");

        DirectoryWriteAccess access = DirectoryWriteProbe.ProbeFileCreation(paths.GameDirectory);
        if (access == DirectoryWriteAccess.Allowed)
        {
            return true;
        }

        if (access == DirectoryWriteAccess.WriteProtected)
        {
            _logger.LogWarning("The {Game} folder is on write-protected media.", paths.Game);
            return false;
        }

        SecurityIdentifier user = GetCurrentUser();
        if (HoldsGrant(paths.GameDirectory, user))
        {
            // Something other than the folder's permissions refuses the file, such as a Deny entry or Controlled
            // Folder Access, so granting again would only repeat the prompt.
            _logger.LogWarning(
                "The {Game} folder refuses new files although write access was already granted.",
                paths.Game);
            return false;
        }

        _logger.LogInformation("The {Game} folder refuses new files; requesting write access.", paths.Game);
        int exitCode;
        try
        {
            exitCode = await _runElevated(CreateGrant(paths.GameDirectory, user), cancellationToken);
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

        if (DirectoryWriteProbe.ProbeFileCreation(paths.GameDirectory) != DirectoryWriteAccess.Allowed)
        {
            _logger.LogWarning("The {Game} folder still refuses new files after write access was granted.", paths.Game);
            return false;
        }

        _logger.LogInformation("Granted write access to the {Game} folder.", paths.Game);
        return true;
    }

    /// <summary>
    ///     Returns the user running the launcher.
    /// </summary>
    /// <remarks>
    ///     Read unelevated: when a standard user approves with administrator credentials, icacls runs as that
    ///     administrator, but the access belongs to the user running the launcher.
    /// </remarks>
    private static SecurityIdentifier GetCurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ??
               throw new InvalidOperationException("The current Windows user has no security identifier.");
    }

    /// <summary>
    ///     Reports whether the folder already carries an explicit Modify grant for the user, as
    ///     <see cref="CreateGrant" /> makes.
    /// </summary>
    private static bool HoldsGrant(string gameDirectory, SecurityIdentifier user)
    {
        // Inherited entries are left out: they come from the parent folder, so a user-owned parent such as a profile
        // folder would otherwise count as a grant on every folder below it.
        return new DirectoryInfo(gameDirectory)
            .GetAccessControl(AccessControlSections.Access)
            .GetAccessRules(true, false, typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .Any(rule => rule.AccessControlType == AccessControlType.Allow &&
                         user.Equals(rule.IdentityReference) &&
                         (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify &&
                         (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0);
    }

    /// <summary>
    ///     Describes the elevated grant.
    /// </summary>
    private static ProcessStartInfo CreateGrant(string gameDirectory, SecurityIdentifier user)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "icacls.exe"))
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add(gameDirectory);
        startInfo.ArgumentList.Add("/grant");
        // Object and container inheritance carry the grant to every file and subfolder that inherits from the folder.
        startInfo.ArgumentList.Add($"*{user.Value}:(OI)(CI)M");
        // If a link replaces the folder while the consent prompt is open, the grant changes the link, not its target.
        startInfo.ArgumentList.Add("/L");
        return startInfo;
    }

    [ExcludeFromCodeCoverage(Justification =
        "Starts an elevated system process; the grant it runs is covered through the injected runner.")]
    private static async Task<int> RunElevatedAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        // The UI thread owns the window the prompt is parented to, whichever thread asks for the grant.
        IntPtr ownerHandle = await Dispatcher.UIThread.InvokeAsync(
            () => AvaloniaDialog.ResolveApplicationOwner(null)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        // .NET hands ShellExecuteEx a parent window only alongside ErrorDialog. Without one, Windows shows the consent
        // prompt behind the launcher as a flashing taskbar button. ErrorDialog also lets Windows show its own error
        // box before the launcher's message when elevation fails for a reason other than cancel, such as a policy
        // that refuses standard users. That rare duplicate is accepted: avoiding it means calling ShellExecuteEx
        // directly, with its own interop and argument quoting that only a manual UAC check could verify.
        startInfo.ErrorDialog = true;
        startInfo.ErrorDialogParentHandle = ownerHandle;
        // ShellExecuteEx returns only after the user answers the consent prompt, so keep it off the UI thread even if
        // the dispatcher call above resumed there.
        using Process process = await Task.Run(() => Process.Start(startInfo), cancellationToken) ??
                                throw new InvalidOperationException("Windows did not start the write access grant.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
