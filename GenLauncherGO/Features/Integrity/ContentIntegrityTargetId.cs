using GenLauncherGO.Features.Mods;

namespace GenLauncherGO.Features.Integrity;

/// <summary>
///     Owns the stable identifiers that bind launcher content identity to its persisted integrity snapshots.
/// </summary>
/// <remarks>
///     Snapshots are stored under a hash of these identifiers, so target construction and snapshot retention must
///     derive them the same way or retention silently stops matching the snapshots it is meant to keep. The hash also
///     makes the identifiers persisted: changing one, prefix included, orphans every existing snapshot and sends each
///     installed managed package back through adoption or repair.
/// </remarks>
internal static class ContentIntegrityTargetId
{
    private const string PackagePrefix = "package";

    /// <summary>
    ///     Builds the identifier for a version's installed package directory.
    /// </summary>
    public static string ForPackage(LauncherContentKey contentKey)
    {
        return string.Concat(PackagePrefix, ":", contentKey.ToStableString());
    }
}
