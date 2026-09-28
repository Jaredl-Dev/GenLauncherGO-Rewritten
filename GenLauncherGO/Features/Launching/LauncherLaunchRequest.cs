using System;
using System.Collections.Generic;
using GenLauncherGO.Features.Mods;

namespace GenLauncherGO.Features.Launching;

internal sealed class LauncherLaunchRequest(
    GameLaunchTargetKind targetKind,
    string executablePath,
    string targetDisplayName,
    string executableDisplayName,
    bool useGeneralsOnline,
    IReadOnlyList<LauncherContentVersion> activeVersions)
{
    public IReadOnlyList<LauncherContentVersion> ActiveVersions { get; } =
        activeVersions ?? throw new ArgumentNullException(nameof(activeVersions));

    public string ExecutablePath { get; } = executablePath ?? string.Empty;

    /// <summary>
    ///     Gets the user-facing name of what is launched: the managed game, or World Builder.
    /// </summary>
    public string TargetDisplayName { get; } = targetDisplayName;

    /// <summary>
    ///     Gets the user-facing name of the selected game client or World Builder option.
    /// </summary>
    public string ExecutableDisplayName { get; } = executableDisplayName;

    public GameLaunchTargetKind TargetKind { get; } = targetKind;

    public bool UseGeneralsOnline { get; } = useGeneralsOnline;
}
