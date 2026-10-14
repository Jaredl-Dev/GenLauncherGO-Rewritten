using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;

namespace GenLauncherGO.Features.Integrity;

/// <summary>
///     Verifies and resolves launch-readiness integrity state for selected launcher content.
/// </summary>
internal interface ILaunchContentIntegrityResolutionService
{
    /// <summary>
    ///     Verifies active launch content and returns the target contexts used for any later resolution.
    /// </summary>
    Task<LaunchContentIntegrityVerificationResult> VerifyAsync(
        LauncherPaths paths,
        IReadOnlyList<LauncherContentVersion> activeVersions,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Captures initial snapshots for untracked managed remote content whose files already match the remote source,
    ///     and reports whether any target was initialized.
    /// </summary>
    /// <remarks>
    ///     Content that was installed by an earlier launcher, or moved in from another one, is byte-identical to what a
    ///     download would produce but carries no snapshot. Adopting it here is what keeps that content out of the launch
    ///     review dialog without redownloading a package the user already has.
    /// </remarks>
    Task<bool> InitializeUntrackedManagedContentAsync(
        LaunchContentIntegrityVerificationResult verification,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Applies confirmed launch-integrity resolutions, including snapshots, cleanup, and package repair.
    /// </summary>
    Task ResolveAsync(
        LaunchContentIntegrityVerificationResult verification,
        IProgress<LaunchContentIntegrityResolutionProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Marks a manually imported version as manual content, and captures its initial package snapshot only when the
    ///     import still resolves to a managed remote package.
    /// </summary>
    Task RegisterManualImportAsync(
        LauncherPaths paths,
        LauncherContentVersion version,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Captures the initial package snapshot for a newly installed managed remote version.
    /// </summary>
    Task CaptureManagedInstallSnapshotAsync(
        LauncherPaths paths,
        LauncherContentVersion version,
        CancellationToken cancellationToken);
}
