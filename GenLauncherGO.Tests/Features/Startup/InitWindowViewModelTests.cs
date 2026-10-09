using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Remote;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Startup;

[Collection("Avalonia")]
public sealed class InitWindowViewModelTests
{
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
                });
            using var themeScope = new ApplicationThemeScope();

            await viewModel.PrepareLauncherAsync();

            steps.Should().Equal("grant", "recover");
        });
    }

    [Fact]
    public void ShutdownAfterStartupStopsAtGameFolderWarning_LeavesInterruptedDeploymentUntouched()
    {
        StaTestRunner.Run(async () =>
        {
            using var directory = new TestDirectory();
            LauncherPaths paths = TestLauncherPaths.Create(directory);
            using var denial = new DeniedFileCreationScope(paths.GameDirectory);
            ILaunchPreparationService launchPreparation = Substitute.For<ILaunchPreparationService>();
            using var themeScope = new ApplicationThemeScope();
            LauncherRuntimeContext runtimeContext = TestLauncherRuntimeContext.Create(paths);
            InitWindowViewModel viewModel = CreateViewModel(
                paths,
                launchPreparation,
                (_, _) => throw new Win32Exception(GameFolderWriteAccess.ErrorCancelled),
                runtimeContext);
            LauncherShutdownCoordinator shutdownCoordinator = new(
                launchPreparation,
                runtimeContext,
                Substitute.For<ILauncherHostEnvironmentService>(),
                Substitute.For<ILauncherApplicationUpdateService>(),
                NullLogger<LauncherShutdownCoordinator>.Instance);

            await viewModel.StartAsync();
            shutdownCoordinator.Shutdown(LauncherRestartKind.None, () => { });

            launchPreparation.ReceivedCalls().Select(call => call.GetMethodInfo().Name).Should().BeEmpty();
        });
    }

    private static InitWindowViewModel CreateViewModel(
        LauncherPaths paths,
        ILaunchPreparationService launchPreparation,
        Func<ProcessStartInfo, CancellationToken, Task<int>> runElevated,
        LauncherRuntimeContext? runtimeContext = null)
    {
        return new InitWindowViewModel(
            Substitute.For<IRemoteConnectionProbe>(),
            launchPreparation,
            new GameFolderWriteAccess(NullLogger<GameFolderWriteAccess>.Instance, runElevated),
            Substitute.For<ILauncherPathResolver>(),
            new FakeLauncherContentCatalog(),
            runtimeContext ?? TestLauncherRuntimeContext.Create(paths),
            new FakeStringLocalizer(),
            new RecordingStartupDialogService());
    }
}
