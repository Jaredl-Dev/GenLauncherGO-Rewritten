using System;
using GenLauncherGO.Features.Updating;

namespace GenLauncherGO.Features.Integrity;

/// <summary>
///     Reports package progress for one launch-integrity resolution target.
/// </summary>
internal sealed record LaunchContentIntegrityResolutionProgress
{
    public LaunchContentIntegrityResolutionProgress(
        string targetId,
        PackageUpdateProgress packageProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(packageProgress);

        TargetId = targetId;
        PackageProgress = packageProgress;
    }

    public string TargetId { get; }

    public PackageUpdateProgress PackageProgress { get; }
}
