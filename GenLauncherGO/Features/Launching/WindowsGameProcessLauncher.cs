using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace GenLauncherGO.Features.Launching;

/// <summary>
///     Launches Windows game and World Builder processes for supported Command &amp; Conquer clients.
/// </summary>
/// <remarks>
///     Each launch runs in its own Windows job object. Windows adds every process the launched program starts to that
///     job, so a launcher that starts the game and exits, such as retail <c>generals.exe</c> starting <c>game.dat</c>,
///     stays tracked until the game itself exits.
/// </remarks>
internal sealed class WindowsGameProcessLauncher : IGameProcessLauncher
{
    /// <summary>
    ///     The observed game-process running time required to treat a launch as successful.
    /// </summary>
    private const int SuccessfulLaunchThresholdMilliseconds = 12000;

    private const int JobPollMilliseconds = 100;

    private const int JobObjectBasicAccountingInformationClass = 1;

    private const uint ForceClosedExitCode = 1;

    private readonly ILogger<WindowsGameProcessLauncher> _logger;

    public WindowsGameProcessLauncher(ILogger<WindowsGameProcessLauncher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IGameProcessLaunchOperation> StartAsync(
        GameLaunchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureExecutableCanLaunch(request.ExecutablePath);
        SafeJobHandle job = await Task.Run(() => StartInJob(request), cancellationToken).ConfigureAwait(false);
        return new WindowsGameProcessLaunchOperation(request, job, _logger);
    }

    private static void EnsureExecutableCanLaunch(string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The selected executable is no longer available.", executablePath);
        }

        if (FileSystemPathSafety.ExistingPathChainContainsReparsePoint(executablePath, "Executable paths"))
        {
            throw new IOException("The selected executable must not be reached through a symbolic link or other reparse point.");
        }
    }

    /// <summary>
    ///     Starts the executable and assigns it to a new job before it can start the game.
    /// </summary>
    /// <remarks>
    ///     The job has no kill-on-close limit, so the game keeps running if the launcher exits. A process the executable
    ///     starts before assignment would not be tracked.
    /// </remarks>
    private static SafeJobHandle StartInJob(GameLaunchRequest request)
    {
        SafeJobHandle job = CreateJobObject(IntPtr.Zero, null);
        try
        {
            if (job.IsInvalid)
            {
                throw new Win32Exception();
            }

            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = request.ExecutablePath,
                Arguments = request.Arguments,
                WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath)!,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException($"Failed to start {Path.GetFileName(request.ExecutablePath)}.");
            if (!AssignProcessToJobObject(job, process.SafeHandle))
            {
                // An untracked game would have its deployed files cleaned up while it is still running.
                var exception = new Win32Exception();
                process.Kill(entireProcessTree: true);
                throw exception;
            }

            return job;
        }
        catch
        {
            job.Dispose();
            throw;
        }
    }

    private static bool HasActiveProcesses(SafeJobHandle job)
    {
        if (!QueryInformationJobObject(
                job,
                JobObjectBasicAccountingInformationClass,
                out JobBasicAccountingInformation information,
                (uint)Marshal.SizeOf<JobBasicAccountingInformation>(),
                IntPtr.Zero))
        {
            throw new Win32Exception();
        }

        return information.ActiveProcesses > 0;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeJobHandle CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeJobHandle job, SafeProcessHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(
        SafeJobHandle job,
        int informationClass,
        out JobBasicAccountingInformation information,
        uint informationLength,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeJobHandle job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>
    ///     Tracks a launch until its job has no running processes and applies the game-start success threshold.
    /// </summary>
    private sealed class WindowsGameProcessLaunchOperation : IGameProcessLaunchOperation
    {
        private readonly string _executableName;

        private readonly SafeJobHandle _job;

        private readonly ILogger<WindowsGameProcessLauncher> _logger;

        private readonly Lock _syncRoot = new();

        private readonly GameLaunchTargetKind _targetKind;

        public WindowsGameProcessLaunchOperation(
            GameLaunchRequest request,
            SafeJobHandle job,
            ILogger<WindowsGameProcessLauncher> logger)
        {
            _targetKind = request.TargetKind;
            _executableName = Path.GetFileName(request.ExecutablePath);
            _job = job;
            _logger = logger;
            Completion = CompleteAsync();
        }

        public Task<bool> Completion { get; }

        public void ForceClose()
        {
            _logger.LogInformation("Force close requested for launched process {ExecutableName}.", _executableName);
            lock (_syncRoot)
            {
                if (_job.IsClosed)
                {
                    return;
                }

                if (!TerminateJobObject(_job, ForceClosedExitCode))
                {
                    _logger.LogWarning(
                        new Win32Exception(),
                        "Failed to force close launched process {ExecutableName}.",
                        _executableName);
                }
            }
        }

        /// <summary>
        ///     Waits until every process in the launch job has exited and applies the launch success policy.
        /// </summary>
        private async Task<bool> CompleteAsync()
        {
            long startedAt = Stopwatch.GetTimestamp();
            try
            {
                while (HasActiveProcesses(_job))
                {
                    await Task.Delay(JobPollMilliseconds).ConfigureAwait(false);
                }
            }
            finally
            {
                lock (_syncRoot)
                {
                    _job.Dispose();
                }
            }

            TimeSpan runningDuration = Stopwatch.GetElapsedTime(startedAt);
            if (_targetKind == GameLaunchTargetKind.GameClient &&
                runningDuration.TotalMilliseconds < SuccessfulLaunchThresholdMilliseconds)
            {
                _logger.LogWarning(
                    "Launch of {ExecutableName} ended after {RunningDurationMs}ms, below the success threshold.",
                    _executableName,
                    runningDuration.TotalMilliseconds);
                return false;
            }

            return true;
        }
    }

    /// <summary>
    ///     Owns a Windows job object handle.
    /// </summary>
    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicAccountingInformation
    {
        public long TotalUserTime;

        public long TotalKernelTime;

        public long ThisPeriodTotalUserTime;

        public long ThisPeriodTotalKernelTime;

        public uint TotalPageFaultCount;

        public uint TotalProcesses;

        public uint ActiveProcesses;

        public uint TotalTerminatedProcesses;
    }
}
