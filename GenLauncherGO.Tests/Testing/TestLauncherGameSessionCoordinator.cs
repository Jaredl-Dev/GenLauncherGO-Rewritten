using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Settings;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.Localization;
using GenLauncherGO.Shared.Remote;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Testing;

internal static class TestLauncherGameSessionCoordinator
{
    public static LauncherGameSessionCoordinator Create(
        LauncherRuntimeContext runtimeContext,
        ILauncherPreferencesService preferencesService,
        ILaunchPreparationService launchPreparationService,
        LauncherPackageActivityService packageActivityService,
        LauncherLaunchCoordinator launchCoordinator,
        ILauncherDialogService dialogService,
        ILauncherStringLocalizer stringLocalizer,
        ILauncherContentCatalog? catalog = null,
        Func<ProcessStartInfo, CancellationToken, Task<int>>? runElevated = null)
    {
        // A failing runner keeps a real administrator prompt out of the test suite.
        Func<ProcessStartInfo, CancellationToken, Task<int>> resolvedRunElevated =
            runElevated ?? ((_, _) => Task.FromResult(1));

        return new LauncherGameSessionCoordinator(
            runtimeContext,
            preferencesService,
            new FakeGameInstallationService(),
            Substitute.For<ILauncherPathResolver>(),
            launchPreparationService,
            new GameFolderWriteAccess(NullLogger<GameFolderWriteAccess>.Instance, resolvedRunElevated),
            Substitute.For<IRemoteConnectionProbe>(),
            catalog ?? new FakeLauncherContentCatalog(),
            packageActivityService,
            launchCoordinator,
            dialogService,
            stringLocalizer,
            NullLogger<LauncherGameSessionCoordinator>.Instance);
    }
}
