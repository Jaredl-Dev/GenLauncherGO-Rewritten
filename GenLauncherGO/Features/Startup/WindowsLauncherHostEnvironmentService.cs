using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Velopack.Locators;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Provides Windows process, single-instance, and foreground-window startup operations.
/// </summary>
internal sealed class WindowsLauncherHostEnvironmentService : ILauncherHostEnvironmentService
{
    private readonly ILogger<WindowsLauncherHostEnvironmentService> _logger;
    private readonly Func<string?> _packagedRootDirectoryResolver;
    private readonly Func<string?> _processPathResolver;
    private readonly Action<TimeSpan> _waitBeforeSingleInstanceRetry;

    public WindowsLauncherHostEnvironmentService()
        : this(NullLogger<WindowsLauncherHostEnvironmentService>.Instance)
    {
    }

    public WindowsLauncherHostEnvironmentService(ILogger<WindowsLauncherHostEnvironmentService> logger)
        : this(logger, Thread.Sleep, ResolveProcessPath, ResolvePackagedRootDirectory)
    {
    }

    internal WindowsLauncherHostEnvironmentService(
        ILogger<WindowsLauncherHostEnvironmentService> logger,
        Action<TimeSpan> waitBeforeSingleInstanceRetry,
        Func<string?>? processPathResolver = null,
        Func<string?>? packagedRootDirectoryResolver = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _waitBeforeSingleInstanceRetry = waitBeforeSingleInstanceRetry ??
                                         throw new ArgumentNullException(nameof(waitBeforeSingleInstanceRetry));
        _processPathResolver = processPathResolver ?? ResolveProcessPath;
        _packagedRootDirectoryResolver = packagedRootDirectoryResolver ?? ResolvePackagedRootDirectory;
    }

    public void ActivateCurrentProcessWindow()
    {
        using var currentProcess = Process.GetCurrentProcess();
        Process? process = Process.GetProcessesByName(currentProcess.ProcessName)
            .FirstOrDefault(candidate => candidate.Id != currentProcess.Id);
        IntPtr windowHandle = process?.MainWindowHandle ?? IntPtr.Zero;

        if (windowHandle == IntPtr.Zero)
        {
            _logger.LogDebug("No existing launcher window was available to activate.");
            return;
        }

        // Windows refuses ShowWindowAsync from a normal process to a launcher running as administrator, so a
        // minimized one would stay minimized. SwitchToThisWindow restores and activates it the way Alt+Tab does.
        // Microsoft says it is not intended for general use. It is used anyway because the supported alternative is
        // a custom window message that the running launcher would have to let through Windows' privilege filter and
        // handle itself, and the function has stayed in user32 since Windows XP.
        SwitchToThisWindow(windowHandle, true);
    }

    public string GetLauncherRootDirectory()
    {
        string? packagedRootDirectory = _packagedRootDirectoryResolver();
        if (!string.IsNullOrWhiteSpace(packagedRootDirectory))
        {
            return packagedRootDirectory;
        }

        string? executablePath = _processPathResolver();

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return AppContext.BaseDirectory;
        }

        return Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
    }

    public LauncherRestartResult TryRestartCurrentProcess(bool asAdministrator)
    {
        try
        {
            string? executablePath = _processPathResolver();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                const string MissingExecutableMessage = "The launcher executable path could not be resolved.";
                _logger.LogError(MissingExecutableMessage);
                return LauncherRestartResult.Failure(MissingExecutableMessage);
            }

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = asAdministrator ? "runas" : string.Empty
            });
            if (process == null)
            {
                const string StartFailureMessage = "Windows did not start the replacement launcher process.";
                _logger.LogError(StartFailureMessage);
                return LauncherRestartResult.Failure(StartFailureMessage);
            }

            process.Dispose();
            _logger.LogInformation(
                "Started a replacement launcher process for restart. As administrator: {AsAdministrator}.",
                asAdministrator);
            return LauncherRestartResult.Success;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not start a replacement launcher process.");
            return LauncherRestartResult.Failure(exception.Message);
        }
    }

    public ILauncherSingleInstanceGuard TryAcquireSingleInstance(string instanceName, TimeSpan retryDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryDelay, TimeSpan.Zero);

        Mutex mutex = new(true, instanceName, out bool createdNew);
        if (createdNew)
        {
            return new MutexSingleInstanceGuard(mutex, true);
        }

        mutex.Dispose();
        if (retryDelay > TimeSpan.Zero)
        {
            _waitBeforeSingleInstanceRetry(retryDelay);
        }

        mutex = new Mutex(true, instanceName, out createdNew);
        if (createdNew)
        {
            return new MutexSingleInstanceGuard(mutex, true);
        }

        mutex.Dispose();
        return MutexSingleInstanceGuard.NotAcquired;
    }

    private static string? ResolveProcessPath()
    {
        return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
    }

    private static string? ResolvePackagedRootDirectory()
    {
        IVelopackLocator locator = VelopackLocator.Current;
        return locator.CurrentlyInstalledVersion == null ? null : locator.RootAppDir;
    }

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAltTab);

    private sealed class MutexSingleInstanceGuard : ILauncherSingleInstanceGuard
    {
        public static readonly MutexSingleInstanceGuard NotAcquired = new(null, false);

        private readonly Mutex? _mutex;

        public MutexSingleInstanceGuard(Mutex? mutex, bool isAcquired)
        {
            _mutex = mutex;
            IsAcquired = isAcquired;
        }

        public bool IsAcquired { get; }

        public void Dispose()
        {
            _mutex?.Dispose();
        }
    }
}
