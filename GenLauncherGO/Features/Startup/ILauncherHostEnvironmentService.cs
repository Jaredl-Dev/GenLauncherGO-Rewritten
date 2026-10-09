using System;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Provides host-process and operating-system operations needed by launcher startup.
/// </summary>
internal interface ILauncherHostEnvironmentService
{
    /// <summary>
    ///     Restores and brings the first visible window for the current process name to the foreground when possible,
    ///     including one running as administrator.
    /// </summary>
    void ActivateCurrentProcessWindow();

    /// <summary>
    ///     Gets the durable launcher root. Packaged builds return the Velopack root rather than the replaceable
    ///     versioned application directory.
    /// </summary>
    string GetLauncherRootDirectory();

    /// <summary>
    ///     Attempts to start a replacement instance of the current launcher process.
    /// </summary>
    LauncherRestartResult TryRestartCurrentProcess();

    /// <summary>
    ///     Attempts to acquire the launcher single-instance guard; the returned guard reports whether startup may continue.
    /// </summary>
    ILauncherSingleInstanceGuard TryAcquireSingleInstance(string instanceName, TimeSpan retryDelay);
}
