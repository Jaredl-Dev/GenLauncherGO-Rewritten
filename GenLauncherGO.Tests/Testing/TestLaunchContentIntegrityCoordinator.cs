using System;
using System.Collections.Generic;
using System.Threading;
using GenLauncherGO.Features.Integrity;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Testing;

/// <summary>
///     Builds the integrity coordinator with a resolution service that reports nothing to fix, which is the
///     arrangement every test that is not about integrity needs.
/// </summary>
internal static class TestLaunchContentIntegrityCoordinator
{
    public static LaunchContentIntegrityCoordinator Create(
        ILaunchContentIntegrityResolutionService? resolutionService = null,
        LauncherRuntimePathContext? runtimePaths = null,
        LauncherPackageActivityService? packageActivityService = null,
        ILauncherDialogService? dialogService = null,
        ILauncherStringLocalizer? stringLocalizer = null)
    {
        LauncherRuntimePathContext effectiveRuntimePaths =
            runtimePaths ?? TestLauncherPaths.CreateRuntimePathContext(TestLauncherPaths.Create());
        return new LaunchContentIntegrityCoordinator(
            resolutionService ?? CreateNoIssueResolutionService(effectiveRuntimePaths.ActivePaths),
            effectiveRuntimePaths,
            packageActivityService ?? new LauncherPackageActivityService(),
            stringLocalizer ?? new FakeStringLocalizer(),
            dialogService ?? Substitute.For<ILauncherDialogService>(),
            NullLogger<LaunchContentIntegrityCoordinator>.Instance);
    }

    private static ILaunchContentIntegrityResolutionService CreateNoIssueResolutionService(LauncherPaths paths)
    {
        ILaunchContentIntegrityResolutionService resolutionService =
            Substitute.For<ILaunchContentIntegrityResolutionService>();
        resolutionService.VerifyAsync(
                Arg.Any<LauncherPaths>(),
                Arg.Any<IReadOnlyList<LauncherContentVersion>>(),
                Arg.Any<CancellationToken>())
            .Returns(new LaunchContentIntegrityVerificationResult(
                paths,
                new ContentIntegrityReport(Array.Empty<ContentIntegrityIssue>()),
                Array.Empty<LaunchContentIntegrityTargetContext>()));
        return resolutionService;
    }
}
