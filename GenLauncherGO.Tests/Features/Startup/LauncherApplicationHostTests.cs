using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using GenLauncherGO.Features.Settings;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Localization;

namespace GenLauncherGO.Tests.Features.Startup;

[Collection("Avalonia")]
public sealed class LauncherApplicationHostTests
{
    /// <summary>
    ///     Preferences in the flat format written before the versioned schema, which loading converts.
    /// </summary>
    private const string UnversionedPreferences =
        """
        LaunchesCount: 7
        AutoDeleteOldVersions: true
        """;

    [Fact]
    public async Task RunWhenLauncher_IsInsideGameStopsBeforeWritingStandaloneStorageAsync()
    {
        using var directory = new TestDirectory();
        AvaloniaLauncherStringLocalizer localizer = new();
        var hostEnvironment = new StubLauncherHostEnvironmentService();
        var storagePaths = new LauncherStoragePaths(directory.Path);
        WriteUnversionedPreferences(storagePaths);
        var pathResolver = new StubLauncherPathResolver
        {
            ResolvedPaths = storagePaths
        };
        var startupDialogService = new RecordingStartupDialogService();
        var startupWorkflow = new StubStandaloneStartupWorkflow
        {
            LauncherLocationBlocked = true
        };
        using LauncherApplicationHost host = new(
            pathResolver,
            hostEnvironment,
            localizer,
            startupDialogService,
            startupWorkflow);

        await host.RunAsync();

        startupWorkflow.LocationChecks.Should().Be(1);
        startupWorkflow.RunCount.Should().Be(0);
        pathResolver.TryPrepareLauncherDirectoriesCount.Should().Be(0);
        Directory.Exists(storagePaths.LogsDirectory).Should().BeFalse();
        File.ReadAllText(storagePaths.PreferencesFilePath).Should().Be(UnversionedPreferences);
        startupDialogService.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task RunWhenLauncherFolder_IsNotWritable_ExplainsAndStopsBeforeSetupAsync()
    {
        using var directory = new TestDirectory();
        AvaloniaLauncherStringLocalizer localizer = new();
        var storagePaths = new LauncherStoragePaths(directory.Path);
        WriteUnversionedPreferences(storagePaths);
        var startupDialogService = new RecordingStartupDialogService();
        var startupWorkflow = new StubStandaloneStartupWorkflow();
        using LauncherApplicationHost host = new(
            new StubLauncherPathResolver
            {
                ResolvedPaths = storagePaths,
                DataFolderWritable = false
            },
            new StubLauncherHostEnvironmentService(),
            localizer,
            startupDialogService,
            startupWorkflow);

        await host.RunAsync();

        startupDialogService.TitledMessages.Should().Equal(
            (localizer["LauncherFolderNotWritable"],
                string.Format(
                    CultureInfo.CurrentCulture,
                    localizer["LauncherFolderNotWritableDescription"],
                    storagePaths.ExecutableDirectory)));
        startupWorkflow.RunCount.Should().Be(0);
        File.ReadAllText(storagePaths.PreferencesFilePath).Should().Be(UnversionedPreferences);
    }

    [Fact]
    public async Task Run_SavesConvertedPreferencesOnceStartupChecksPassAsync()
    {
        using var directory = new TestDirectory();
        var storagePaths = new LauncherStoragePaths(directory.Path);
        WriteUnversionedPreferences(storagePaths);
        using LauncherApplicationHost host = new(
            new StubLauncherPathResolver { ResolvedPaths = storagePaths },
            new StubLauncherHostEnvironmentService(),
            new AvaloniaLauncherStringLocalizer(),
            new RecordingStartupDialogService(),
            new StubStandaloneStartupWorkflow());

        await host.RunAsync();

        File.ReadAllText(storagePaths.PreferencesFilePath).Should()
            .Contain($"SchemaVersion: {LauncherPreferencesDocument.CurrentSchemaVersion}");
    }

    [Fact]
    public async Task Run_UsesInjectedWorkflowInStandaloneStartupOrderAsync()
    {
        using var directory = new TestDirectory();
        var events = new List<string>();
        AvaloniaLauncherStringLocalizer localizer = new();
        var pathResolver = new StubLauncherPathResolver
        {
            Events = events,
            ResolvedPaths = new LauncherStoragePaths(directory.Path)
        };
        var hostEnvironment = new StubLauncherHostEnvironmentService
        {
            Events = events
        };
        var startupWorkflow = new StubStandaloneStartupWorkflow
        {
            Events = events
        };
        using LauncherApplicationHost host = new(
            pathResolver,
            hostEnvironment,
            localizer,
            new RecordingStartupDialogService(),
            startupWorkflow);

        await host.RunAsync();

        events.Should().Equal(
            "resolve-storage",
            "location",
            "prepare-storage",
            "single-instance",
            "setup");
        startupWorkflow.LocationChecks.Should().Be(1);
        startupWorkflow.RunCount.Should().Be(1);
    }

    [Fact]
    public async Task RunWhenAnotherInstanceOwnsGuard_ActivatesExistingWindowAndSkipsSetupAsync()
    {
        using var directory = new TestDirectory();
        var storagePaths = new LauncherStoragePaths(directory.Path);
        WriteUnversionedPreferences(storagePaths);
        var pathResolver = new StubLauncherPathResolver
        {
            ResolvedPaths = storagePaths
        };
        var hostEnvironment = new StubLauncherHostEnvironmentService
        {
            SingleInstanceAcquired = false
        };
        var startupWorkflow = new StubStandaloneStartupWorkflow();
        using LauncherApplicationHost host = new(
            pathResolver,
            hostEnvironment,
            new AvaloniaLauncherStringLocalizer(),
            new RecordingStartupDialogService(),
            startupWorkflow);

        await host.RunAsync();

        pathResolver.TryPrepareLauncherDirectoriesCount.Should().Be(1);
        hostEnvironment.ActivationCount.Should().Be(1);
        startupWorkflow.RunCount.Should().Be(0);
        File.ReadAllText(storagePaths.PreferencesFilePath).Should().Be(UnversionedPreferences);
    }

    [Fact]
    public void Start_HandlesDispatcherFailuresRaisedDuringStandaloneSetup()
    {
        StaTestRunner.Run(async () =>
        {
            using var directory = new TestDirectory();
            var expectedException = new InvalidOperationException("Picker failed during setup.");
            AvaloniaLauncherStringLocalizer localizer = new();
            var pathResolver = new StubLauncherPathResolver
            {
                ResolvedPaths = new LauncherStoragePaths(directory.Path)
            };
            var startupDialogService = new RecordingStartupDialogService();
            var startupWorkflow = new StubStandaloneStartupWorkflow
            {
                DispatcherException = expectedException
            };
            using LauncherApplicationHost host = new(
                pathResolver,
                new StubLauncherHostEnvironmentService(),
                localizer,
                startupDialogService,
                startupWorkflow);
            IClassicDesktopStyleApplicationLifetime desktop =
                Substitute.For<IClassicDesktopStyleApplicationLifetime>();

            bool started = await host.StartAsync(desktop);
            await Dispatcher.UIThread.InvokeAsync(() => { });

            started.Should().BeFalse();
            startupDialogService.Messages.Should()
                .ContainSingle(message => message.Contains(expectedException.Message, StringComparison.Ordinal));
        });
    }

    private static void WriteUnversionedPreferences(LauncherStoragePaths storagePaths)
    {
        Directory.CreateDirectory(storagePaths.DataDirectory);
        File.WriteAllText(storagePaths.PreferencesFilePath, UnversionedPreferences);
    }

    private sealed class StubLauncherPathResolver : ILauncherPathResolver
    {
        public List<string>? Events { get; init; }

        public LauncherStoragePaths? ResolvedPaths { get; init; }

        public bool DataFolderWritable { get; init; } = true;

        public int TryPrepareLauncherDirectoriesCount { get; private set; }

        public LauncherStoragePaths Resolve(string executableDirectory)
        {
            Events?.Add("resolve-storage");
            return ResolvedPaths ??
                   throw new InvalidOperationException("The standalone storage path could not be resolved.");
        }

        public bool TryPrepareLauncherDirectories(LauncherStoragePaths paths)
        {
            Events?.Add("prepare-storage");
            TryPrepareLauncherDirectoriesCount++;
            return DataFolderWritable;
        }

        public void PrepareGameDirectories(LauncherPaths paths, bool cleanTemporaryDirectory)
        {
        }
    }

    private sealed class StubLauncherHostEnvironmentService : ILauncherHostEnvironmentService
    {
        public List<string>? Events { get; init; }

        public string ExecutableDirectory { get; } = @"C:\Launcher";

        public bool SingleInstanceAcquired { get; init; } = true;

        public int ActivationCount { get; private set; }

        public int RestartCount { get; private set; }

        /// <summary>
        ///     How often the acquired single-instance guard was disposed, which the launcher only does immediately
        ///     before starting its replacement process.
        /// </summary>
        public int ReleasedGuardCount { get; private set; }

        public void ActivateCurrentProcessWindow()
        {
            ActivationCount++;
        }

        public string GetLauncherRootDirectory()
        {
            return ExecutableDirectory;
        }

        public LauncherRestartResult TryRestartCurrentProcess(bool asAdministrator)
        {
            RestartCount++;
            return LauncherRestartResult.Success;
        }

        public ILauncherSingleInstanceGuard TryAcquireSingleInstance(string instanceName, TimeSpan retryDelay)
        {
            Events?.Add("single-instance");
            return new StubLauncherSingleInstanceGuard(
                SingleInstanceAcquired,
                () => ReleasedGuardCount++);
        }
    }

    private sealed class StubLauncherSingleInstanceGuard(bool isAcquired, Action onDisposed)
        : ILauncherSingleInstanceGuard
    {
        public bool IsAcquired { get; } = isAcquired;

        public void Dispose()
        {
            onDisposed();
        }
    }

    private sealed class StubStandaloneStartupWorkflow : IStandaloneStartupWorkflow
    {
        public List<string>? Events { get; init; }

        public bool LauncherLocationBlocked { get; init; }

        public Exception? DispatcherException { get; init; }

        /// <summary>
        ///     The outcome standalone setup reports, defaulting to the user cancelling it.
        /// </summary>
        public StandaloneStartupResult Result { get; init; } = StandaloneStartupResult.Canceled;

        public int LocationChecks { get; private set; }

        public int RunCount { get; private set; }

        public Task<bool> ShowBlockingLauncherLocationAsync(
            LauncherStoragePaths storagePaths)
        {
            Events?.Add("location");
            LocationChecks++;
            return Task.FromResult(LauncherLocationBlocked);
        }

        public async Task<StandaloneStartupResult> RunAsync(
            LauncherStoragePaths storagePaths,
            ILauncherPreferencesService preferencesService)
        {
            Events?.Add("setup");
            RunCount++;
            if (DispatcherException != null)
            {
                Exception exception = DispatcherException;
                Dispatcher.UIThread.Post(() => throw exception);
                await Dispatcher.UIThread.InvokeAsync(() => { });
            }

            return Result;
        }
    }
}
