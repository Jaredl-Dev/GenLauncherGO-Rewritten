using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Integrity;
using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Dialogs;

namespace GenLauncherGO.Tests.Features.Launcher;

public sealed partial class LauncherWindowWorkflowCoordinatorTests
{
    private const int ErrorElevationRequired = 740;

    [Fact]
    public void LaunchAsyncWhenWindowsRequiresElevation_OffersAdministratorRestart()
    {
        StaTestRunner.Run(async () =>
        {
            ILauncherDialogService dialogService = StubLauncherDialogService.AnsweringWarningConfirmations(true);
            IGameProcessLauncher processLauncher = Substitute.For<IGameProcessLauncher>();
            processLauncher.StartAsync(Arg.Any<GameLaunchRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<IGameProcessLaunchOperation>(
                    new Win32Exception(ErrorElevationRequired)));
            LauncherWindowWorkflowCoordinator coordinator = CreateCoordinator(
                out LauncherRestartCoordinator restartCoordinator,
                dialogService: dialogService,
                gameProcessLauncher: processLauncher);
            WorkflowFixture fixture = new();
            fixture.ViewModel.SelectedGameClientOption = new ExecutableOption(
                "Generals",
                "generals.exe",
                true,
                true);

            await coordinator.LaunchAsync(
                GameLaunchTargetKind.GameClient,
                fixture.Context,
                CancellationToken.None);

            await dialogService.Received(1).ShowWarningConfirmationAsync(
                Arg.Is<LauncherInfoDialogRequest>(request =>
                    request.DetailMessage.Contains("generals.exe", StringComparison.Ordinal)),
                "Restart as administrator",
                fixture.Owner);
            restartCoordinator.RestartKind.Should().Be(LauncherRestartKind.Administrator);
        });
    }

    [Fact]
    public void LaunchAsyncWhenExecutable_IsUnavailableShowsErrorAndDoesNotLaunch()
    {
        StaTestRunner.Run(async () =>
        {
            IGameExecutableDiscoveryService executableDiscovery = Substitute.For<IGameExecutableDiscoveryService>();
            executableDiscovery.IsExecutableAvailable(Arg.Any<string?>()).Returns(false);
            ILauncherDialogService dialogService = Substitute.For<ILauncherDialogService>();
            IGameProcessLauncher processLauncher = Substitute.For<IGameProcessLauncher>();
            LauncherWindowWorkflowCoordinator coordinator = CreateCoordinator(
                dialogService: dialogService,
                executableDiscovery: executableDiscovery,
                gameProcessLauncher: processLauncher);
            WorkflowFixture fixture = new();
            fixture.ViewModel.SelectedGameClientOption = new ExecutableOption(
                "Generals",
                "generals.exe",
                false,
                true);

            await coordinator.LaunchAsync(
                GameLaunchTargetKind.GameClient,
                fixture.Context,
                CancellationToken.None);

            await dialogService.Received(1).ShowErrorAsync(
                Arg.Any<LauncherInfoDialogRequest>(),
                fixture.Owner);
            await processLauncher.DidNotReceive().StartAsync(
                Arg.Any<GameLaunchRequest>(),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void LaunchAsyncForGameWhenProcessSucceeds_EnablesSelectedModificationSupport()
    {
        StaTestRunner.Run(async () =>
        {
            LauncherContentVersion selectedVersion = CreateVersion("Shockwave", ContentSourceKind.Manual);
            FakeLauncherContentCatalog catalog = CreateCatalog();
            catalog.Data.AddOrUpdate(selectedVersion);
            LauncherWindowWorkflowCoordinator coordinator = CreateCoordinator(catalog: catalog);
            WorkflowFixture fixture = new(catalog);
            ModificationViewModel selectedTile = CreateTile(catalog.Data.GetSelectedMod()!);
            fixture.AddTile(selectedTile);
            selectedTile.IsSelected = true;
            fixture.ViewModel.SelectedGameClientOption = new ExecutableOption(
                "Generals",
                "generals.exe",
                true,
                true);

            await coordinator.LaunchAsync(
                GameLaunchTargetKind.GameClient,
                fixture.Context,
                CancellationToken.None);

            selectedTile.SupportButtonBlinking.Should().BeTrue();
        });
    }
}
