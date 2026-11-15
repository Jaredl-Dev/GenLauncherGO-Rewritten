using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Dialogs;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Testing;

internal static class TestLauncherCloseGuard
{
    public static LauncherCloseGuard Create(
        LauncherPackageActivityService packageActivityService,
        ILauncherDialogService? dialogService = null,
        LauncherLaunchCoordinator? launchCoordinator = null)
    {
        ILauncherDialogService resolvedDialogService = dialogService ?? Substitute.For<ILauncherDialogService>();
        return new LauncherCloseGuard(
            launchCoordinator ?? TestLauncherLaunchCoordinator.Create(
                packageActivityService,
                dialogService: resolvedDialogService),
            packageActivityService,
            resolvedDialogService,
            new FakeStringLocalizer(),
            NullLogger<LauncherCloseGuard>.Instance);
    }
}
