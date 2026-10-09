using System.Collections.Generic;
using System.Threading;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Startup;

public sealed class LauncherShutdownCoordinatorTests
{
    [Fact]
    public void ShutdownForApplicationUpdate_CleansAndReleasesGuardBeforeUpdaterHandoff()
    {
        var events = new List<string>();
        LauncherShutdownCoordinator coordinator = CreateCoordinator(events, updateHandoffSucceeded: true);

        coordinator.Shutdown(
            TestLauncherPaths.Create(),
            LauncherRestartKind.ApplicationUpdate,
            () => events.Add("release-single-instance"));

        events.Should().Equal("cleanup", "release-single-instance", "update-handoff");
    }

    [Fact]
    public void ShutdownWhenApplicationUpdateHandoffFails_StartsNormalRestartAfterCleanupAndRelease()
    {
        var events = new List<string>();
        LauncherShutdownCoordinator coordinator = CreateCoordinator(events, updateHandoffSucceeded: false);

        coordinator.Shutdown(
            TestLauncherPaths.Create(),
            LauncherRestartKind.ApplicationUpdate,
            () => events.Add("release-single-instance"));

        events.Should().Equal(
            "cleanup",
            "release-single-instance",
            "update-handoff",
            "normal-restart");
    }

    [Fact]
    public void ShutdownForAdministratorRestart_StartsElevatedReplacementAfterCleanupAndRelease()
    {
        var events = new List<string>();
        LauncherShutdownCoordinator coordinator = CreateCoordinator(events, administratorRestartSucceeded: true);

        coordinator.Shutdown(
            TestLauncherPaths.Create(),
            LauncherRestartKind.Administrator,
            () => events.Add("release-single-instance"));

        events.Should().Equal("cleanup", "release-single-instance", "administrator-restart");
    }

    [Fact]
    public void ShutdownWhenAdministratorRestartFails_FallsBackToNormalRestart()
    {
        var events = new List<string>();
        LauncherShutdownCoordinator coordinator = CreateCoordinator(events, administratorRestartSucceeded: false);

        coordinator.Shutdown(
            TestLauncherPaths.Create(),
            LauncherRestartKind.Administrator,
            () => events.Add("release-single-instance"));

        events.Should().Equal(
            "cleanup",
            "release-single-instance",
            "administrator-restart",
            "normal-restart");
    }

    private static LauncherShutdownCoordinator CreateCoordinator(
        ICollection<string> events,
        bool updateHandoffSucceeded = false,
        bool administratorRestartSucceeded = false)
    {
        ILaunchPreparationService launchPreparationService = Substitute.For<ILaunchPreparationService>();
        launchPreparationService.Cleanup(Arg.Any<LauncherPaths>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                events.Add("cleanup");
                return true;
            });
        ILauncherApplicationUpdateService updateService = Substitute.For<ILauncherApplicationUpdateService>();
        updateService.TryHandoffToUpdateAndRestart().Returns(_ =>
        {
            events.Add("update-handoff");
            return updateHandoffSucceeded;
        });
        ILauncherHostEnvironmentService hostEnvironmentService = Substitute.For<ILauncherHostEnvironmentService>();
        hostEnvironmentService.TryRestartCurrentProcess(false).Returns(_ =>
        {
            events.Add("normal-restart");
            return LauncherRestartResult.Success;
        });
        hostEnvironmentService.TryRestartCurrentProcess(true).Returns(_ =>
        {
            events.Add("administrator-restart");
            return administratorRestartSucceeded
                ? LauncherRestartResult.Success
                : LauncherRestartResult.Failure("The operation was canceled by the user.");
        });

        return new LauncherShutdownCoordinator(
            launchPreparationService,
            hostEnvironmentService,
            updateService,
            NullLogger<LauncherShutdownCoordinator>.Instance);
    }
}
