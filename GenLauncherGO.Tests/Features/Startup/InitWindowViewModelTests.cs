using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Remote;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Startup;

[Collection("Avalonia")]
public sealed class InitWindowViewModelTests
{
    private const int ErrorCancelled = 1223;

    [Fact]
    public void PrepareLauncherAsync_WhenGameFolderRefusesNewFiles_GrantsAccessBeforeRecovery()
    {
        StaTestRunner.Run(async () =>
        {
            using var directory = new TestDirectory();
            LauncherPaths paths = TestLauncherPaths.Create(directory);
            using var denial = new DeniedFileCreationScope(paths.GameDirectory);
            List<string> steps = [];
            ILaunchPreparationService launchPreparation = Substitute.For<ILaunchPreparationService>();
            launchPreparation.Recover(paths, Arg.Any<CancellationToken>()).Returns(_ =>
            {
                steps.Add("recover");
                return true;
            });
            InitWindowViewModel viewModel = CreateViewModel(
                paths,
                launchPreparation,
                (_, _) =>
                {
                    steps.Add("grant");
                    denial.Dispose();
                    return Task.FromResult(0);
                },
                new RecordingStartupDialogService());
            using var themeScope = new ApplicationThemeScope();

            await viewModel.PrepareLauncherAsync();

            steps.Should().Equal("grant", "recover");
        });
    }

    [Fact]
    public async Task PrepareLauncherAsync_WhenElevationIsCanceled_ShowsGameFolderAccessMessageAndStopsAsync()
    {
        using var directory = new TestDirectory();
        LauncherPaths paths = TestLauncherPaths.Create(directory);
        using var denial = new DeniedFileCreationScope(paths.GameDirectory);
        ILaunchPreparationService launchPreparation = Substitute.For<ILaunchPreparationService>();
        var dialogs = new RecordingStartupDialogService { RetryResult = false };
        var stringLocalizer = new FakeStringLocalizer();
        InitWindowViewModel viewModel = CreateViewModel(
            paths,
            launchPreparation,
            (_, _) => Task.FromException<int>(new Win32Exception(ErrorCancelled)),
            dialogs);
        bool shutdownRequested = false;
        viewModel.ShutdownRequested += (_, _) => shutdownRequested = true;

        await viewModel.PrepareLauncherAsync();

        dialogs.RetryCancelWarnings.Should().Equal((
            stringLocalizer["GameFolderAccessDenied"],
            string.Format(
                CultureInfo.CurrentCulture,
                stringLocalizer["GameFolderAccessDeniedDescription"],
                paths.GameDirectory)));
        shutdownRequested.Should().BeTrue();
        launchPreparation.DidNotReceive().Recover(Arg.Any<LauncherPaths>(), Arg.Any<CancellationToken>());
    }

    private static InitWindowViewModel CreateViewModel(
        LauncherPaths paths,
        ILaunchPreparationService launchPreparation,
        Func<ProcessStartInfo, CancellationToken, Task<int>> runElevated,
        IStartupDialogService dialogs)
    {
        return new InitWindowViewModel(
            Substitute.For<IRemoteConnectionProbe>(),
            launchPreparation,
            new GameFolderWriteAccess(NullLogger<GameFolderWriteAccess>.Instance, runElevated),
            Substitute.For<ILauncherPathResolver>(),
            new FakeLauncherContentCatalog(),
            TestLauncherRuntimeContext.Create(paths),
            new FakeStringLocalizer(),
            dialogs);
    }
}
