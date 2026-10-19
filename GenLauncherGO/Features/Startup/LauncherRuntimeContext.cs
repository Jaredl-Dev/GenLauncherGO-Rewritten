using System;
using GenLauncherGO.Shared.Themes;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Stores mutable runtime state shared by launcher startup and Avalonia views.
/// </summary>
internal sealed class LauncherRuntimeContext
{
    public LauncherRuntimeContext(LauncherRuntimePathContext runtimePaths, string currentLauncherVersion)
    {
        RuntimePaths = runtimePaths ?? throw new ArgumentNullException(nameof(runtimePaths));
        CurrentLauncherVersion = string.IsNullOrWhiteSpace(currentLauncherVersion)
            ? LauncherApplicationDefaults.UnavailableLauncherVersion
            : currentLauncherVersion;
    }

    public string CurrentLauncherVersion { get; }

    /// <summary>
    ///     Gets the product name and version the launcher shows and copies for bug reports, such as
    ///     "GenLauncherGO 1.1.1".
    /// </summary>
    public string LauncherVersionLabel => $"GenLauncherGO {CurrentLauncherVersion}";

    /// <summary>
    ///     Gets or sets the active launcher theme.
    /// </summary>
    /// <remarks>
    ///     Assigning also publishes the theme to application-scoped resources, so every open window follows through
    ///     <c>DynamicResource</c> without being told. This is what makes the assignment the single place a theme can
    ///     change; windows that deliberately preview a different palette shadow it with their own resources instead.
    ///     Publishing is skipped when no Avalonia application is running, which is the case in plain unit tests.
    /// </remarks>
    public ColorsInfo Colors
    {
        get => field ??
               throw new InvalidOperationException("Launcher theme has not been initialized.");
        set
        {
            field = value ?? throw new ArgumentNullException(nameof(value));
            LauncherThemeResourceApplier.ApplyToApplication(value);
        }
    }

    /// <summary>
    ///     Gets the resolved launcher paths for the active session.
    /// </summary>
    public LauncherPaths LauncherPaths => RuntimePaths.ActivePaths;

    /// <summary>
    ///     Gets the active-path authority shared by path-sensitive services.
    /// </summary>
    public LauncherRuntimePathContext RuntimePaths { get; }

    /// <summary>
    ///     Gets the standalone launcher's shared storage paths.
    /// </summary>
    public LauncherStoragePaths StoragePaths => RuntimePaths.StoragePaths;

    public string? ModsRepositoryUrl { get; set; }

    public SupportedGame CurrentlyManagedGame => LauncherPaths.Game;

    public bool Connected { get; set; }

    /// <summary>
    ///     Gets or sets whether startup confirmed write access to the active game folder and recovered its deployment.
    /// </summary>
    /// <remarks>
    ///     Shutdown restores the game folder only once this is set. A startup the user stopped at the write-access or
    ///     recovery warning leaves an interrupted deployment for the next start, which asks for access first.
    /// </remarks>
    public bool GameFolderRecovered { get; set; }

    /// <summary>
    ///     Applies the validated active game's repository and visual defaults as one session update.
    /// </summary>
    public void ApplyActiveGameDefaults()
    {
        SupportedGame managedGame = LauncherPaths.Game;
        ModsRepositoryUrl = managedGame switch
        {
            SupportedGame.ZeroHour => LauncherApplicationDefaults.ZeroHourRepositoryUrl,
            SupportedGame.Generals => LauncherApplicationDefaults.GeneralsRepositoryUrl,
            _ => null
        };

        Colors = LauncherThemePresets.Create(managedGame);
    }
}
