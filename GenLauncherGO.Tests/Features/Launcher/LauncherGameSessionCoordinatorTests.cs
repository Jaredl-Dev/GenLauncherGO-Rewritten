using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Settings;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.Remote;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Launcher;

[Collection("Avalonia")]
public sealed class LauncherGameSessionCoordinatorTests
{
    private const int ErrorCancelled = 1223;

    [Fact]
    public void SwitchGameAsync_WhenElevationIsCanceled_KeepsCurrentSession()
    {
        StaTestRunner.Run(async () =>
        {
            using var directory = new TestDirectory();
            (LauncherRuntimePathContext runtimePaths, LauncherPaths generals, LauncherPaths zeroHour) =
                TestLauncherPaths.CreateTwoGameRuntime(directory);
            using var denial = new DeniedFileCreationScope(generals.GameDirectory);
            var runtimeContext = new LauncherRuntimeContext(runtimePaths, TestLauncherRuntimeContext.LauncherVersion)
            {
                Colors = TestLauncherTheme.Create()
            };
            var preferences = new RecordingLauncherPreferencesService(new LauncherPreferences
            {
                Installations = new LauncherInstallations
                {
                    Generals = generals.GameDirectory,
                    ZeroHour = zeroHour.GameDirectory
                },
                LastSelectedGame = SupportedGame.ZeroHour
            });
            ILaunchPreparationService launchPreparation = Substitute.For<ILaunchPreparationService>();
            ILauncherDialogService dialogService = Substitute.For<ILauncherDialogService>();
            var stringLocalizer = new FakeStringLocalizer();
            var packageActivity = new LauncherPackageActivityService();
            var coordinator = new LauncherGameSessionCoordinator(
                runtimeContext,
                preferences,
                new FakeGameInstallationService(),
                Substitute.For<ILauncherPathResolver>(),
                launchPreparation,
                new GameFolderWriteAccess(
                    NullLogger<GameFolderWriteAccess>.Instance,
                    (_, _) => Task.FromException<int>(new Win32Exception(ErrorCancelled))),
                Substitute.For<IRemoteConnectionProbe>(),
                new FakeLauncherContentCatalog(),
                packageActivity,
                TestLauncherLaunchCoordinator.Create(packageActivity, preferences, runtimePaths: runtimePaths),
                dialogService,
                stringLocalizer,
                NullLogger<LauncherGameSessionCoordinator>.Instance);
            string expectedDetails = string.Format(
                CultureInfo.CurrentCulture,
                stringLocalizer["GameSwitchFailedDetails"],
                string.Format(
                    CultureInfo.CurrentCulture,
                    stringLocalizer["GameFolderAccessDeniedDescription"],
                    generals.GameDirectory));

            bool switched = await coordinator.SwitchGameAsync(SupportedGame.Generals, new Window(), CancellationToken.None);

            switched.Should().BeFalse();
            runtimePaths.ActivePaths.Should().Be(zeroHour);
            preferences.Current.LastSelectedGame.Should().Be(SupportedGame.ZeroHour);
            launchPreparation.DidNotReceive().Cleanup(Arg.Any<LauncherPaths>(), Arg.Any<CancellationToken>());
            await dialogService.Received(1).ShowErrorAsync(
                Arg.Is<LauncherInfoDialogRequest>(request => request.DetailMessage == expectedDetails),
                Arg.Any<Window?>());
        });
    }
}
