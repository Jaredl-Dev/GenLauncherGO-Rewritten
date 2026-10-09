using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Settings;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Dialogs;

namespace GenLauncherGO.Tests.Features.Launcher;

[Collection("Avalonia")]
public sealed class LauncherGameSessionCoordinatorTests
{
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
            var packageActivity = new LauncherPackageActivityService();
            LauncherGameSessionCoordinator coordinator = TestLauncherGameSessionCoordinator.Create(
                runtimeContext,
                preferences,
                launchPreparation,
                packageActivity,
                TestLauncherLaunchCoordinator.Create(packageActivity, preferences, runtimePaths: runtimePaths),
                Substitute.For<ILauncherDialogService>(),
                new FakeStringLocalizer(),
                runElevated: (_, _) =>
                    Task.FromException<int>(new Win32Exception(GameFolderWriteAccess.ErrorCancelled)));

            bool switched = await coordinator.SwitchGameAsync(SupportedGame.Generals, new Window(), CancellationToken.None);

            switched.Should().BeFalse();
            runtimePaths.ActivePaths.Should().Be(zeroHour);
            preferences.Current.LastSelectedGame.Should().Be(SupportedGame.ZeroHour);
            launchPreparation.DidNotReceive().Cleanup(Arg.Any<LauncherPaths>(), Arg.Any<CancellationToken>());
        });
    }
}
