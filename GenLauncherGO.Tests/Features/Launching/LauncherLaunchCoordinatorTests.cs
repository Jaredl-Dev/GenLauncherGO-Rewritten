using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Startup;

namespace GenLauncherGO.Tests.Features.Launching;

[Collection("Avalonia")]
public sealed class LauncherLaunchCoordinatorTests
{
    [Fact]
    public void LaunchAsync_CleansUpDeploymentOnlyAfterTheGameExits()
    {
        StaTestRunner.Run(async () =>
        {
            List<string> events = [];
            ILaunchPreparationService preparationService =
                TestLauncherLaunchCoordinator.CreateSuccessfulPreparationService();
            preparationService
                .When(service => service.Cleanup(Arg.Any<LauncherPaths>(), Arg.Any<CancellationToken>()))
                .Do(_ => events.Add("cleanup"));
            ControllableGameProcessLaunch game = new();
            LauncherLaunchCoordinator launchCoordinator = TestLauncherLaunchCoordinator.Create(
                preparationService: preparationService,
                processLauncher: game.Launcher);
            game.Start(launchCoordinator, new Window());

            await game.WaitUntilRunningAsync();
            // Gives an early cleanup time to run, so it would be recorded before the game exits.
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            events.Add("game-exited");
            await game.ExitAsync();

            events.Should().Equal("game-exited", "cleanup");
        });
    }

    [Fact]
    public void LaunchAsync_WhenTheGameCannotStart_StillCleansUpDeployment()
    {
        StaTestRunner.Run(async () =>
        {
            ILaunchPreparationService preparationService =
                TestLauncherLaunchCoordinator.CreateSuccessfulPreparationService();
            IGameProcessLauncher processLauncher = Substitute.For<IGameProcessLauncher>();
            processLauncher.StartAsync(Arg.Any<GameLaunchRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<IGameProcessLaunchOperation>(
                    new InvalidOperationException("The game could not be started.")));
            LauncherLaunchCoordinator launchCoordinator = TestLauncherLaunchCoordinator.Create(
                preparationService: preparationService,
                processLauncher: processLauncher);

            Func<Task> launch = () => TestLauncherLaunchCoordinator.LaunchGameClientAsync(
                launchCoordinator,
                new Window());

            await launch.Should().ThrowAsync<InvalidOperationException>();
            preparationService.Received(1).Cleanup(Arg.Any<LauncherPaths>(), Arg.Any<CancellationToken>());
        });
    }
}
