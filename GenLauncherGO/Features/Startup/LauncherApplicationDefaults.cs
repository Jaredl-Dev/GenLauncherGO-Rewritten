using System;
using System.Linq;
using System.Reflection;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Stores launcher constants used by the Avalonia startup and shell UI.
/// </summary>
internal static class LauncherApplicationDefaults
{
    internal const string GeneralsOnlineDiscordUrl = "https://discord.playgenerals.online/";

    internal const string GeneralsRepositoryUrl =
        "https://raw.githubusercontent.com/p0ls3r/GenLauncherModsData/master/ReposModificationDataGenerals3.yaml";

    internal const int LaunchesCountForUpdateAdvertising = 100;

    internal const string UnavailableLauncherVersion = "Failed to retrieve";

    internal const string ZeroHourRepositoryUrl =
        "https://raw.githubusercontent.com/p0ls3r/GenLauncherModsData/master/ReposModificationDataZH3.yaml";

    private const string LauncherRepositoryUrlMetadataName = "VelopackUpdateRepositoryUrl";

    /// <summary>
    ///     Returns the launcher's own GitHub repository, which also publishes its application updates.
    /// </summary>
    /// <remarks>
    ///     The URL is stamped into the assembly from the <c>VelopackUpdateRepositoryUrl</c> build property, so the
    ///     update feed and the Settings link cannot point at different repositories.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the build did not stamp the URL.</exception>
    internal static string GetLauncherRepositoryUrl()
    {
        return typeof(LauncherApplicationDefaults).Assembly
                   .GetCustomAttributes<AssemblyMetadataAttribute>()
                   .Single(attribute =>
                       string.Equals(attribute.Key, LauncherRepositoryUrlMetadataName, StringComparison.Ordinal))
                   .Value
               ?? throw new InvalidOperationException("The launcher repository URL assembly metadata is missing.");
    }
}
