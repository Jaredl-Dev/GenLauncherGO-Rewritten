namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Describes the palette a published modification asks the launcher to wear while it is selected.
/// </summary>
/// <remarks>
///     The remote manifest's <c>ColorsInformation</c> mapping reads into this type directly, so the property names are
///     part of the legacy backend's external contract and must not be renamed; they are also the launcher's own resource
///     key names. Values stay as the raw strings the backend published: they are colour literals in a presentation
///     format retained for transport and persistence. The theme resolver parses them and applies per-slot fallback when
///     building the Avalonia palette. Any slot may be absent, and a modification may supply none at all. Palette caches
///     written by earlier versions also contain the computed properties, which loading skips.
/// </remarks>
internal sealed class LauncherContentTheme
{
    public string GenLauncherBorderColor { get; set; } = string.Empty;

    public string GenLauncherInactiveBorder { get; set; } = string.Empty;

    public string GenLauncherInactiveBorder2 { get; set; } = string.Empty;

    public string GenLauncherActiveColor { get; set; } = string.Empty;

    public string GenLauncherDarkFillColor { get; set; } = string.Empty;

    public string GenLauncherDarkBackGround { get; set; } = string.Empty;

    public string GenLauncherLightBackGround { get; set; } = string.Empty;

    public string GenLauncherDefaultTextColor { get; set; } = string.Empty;

    public string GenLauncherDownloadTextColor { get; set; } = string.Empty;

    /// <summary>
    ///     Gets the colour the list-selection gradient starts from.
    /// </summary>
    /// <remarks>
    ///     The remote name is <c>...Color1</c>. Upstream swapped this with <c>...Color2</c> on the manifest path only,
    ///     so a published mod's documented start colour actually rendered at the far stop. This launcher maps the
    ///     names as documented instead, which is the behaviour mod authors were writing against.
    /// </remarks>
    public string GenLauncherListBoxSelectionColor1 { get; set; } = string.Empty;

    public string GenLauncherListBoxSelectionColor2 { get; set; } = string.Empty;

    public string GenLauncherButtonSelectionColor { get; set; } = string.Empty;

    /// <summary>
    ///     Gets the absolute URL of the artwork drawn behind the launcher shell.
    /// </summary>
    public string GenLauncherBackgroundImageLink { get; set; } = string.Empty;

    /// <summary>
    ///     Gets a value indicating whether this theme publishes at least one usable palette or artwork value.
    /// </summary>
    public bool HasValues =>
        !string.IsNullOrWhiteSpace(GenLauncherBorderColor) ||
        !string.IsNullOrWhiteSpace(GenLauncherInactiveBorder) ||
        !string.IsNullOrWhiteSpace(GenLauncherInactiveBorder2) ||
        !string.IsNullOrWhiteSpace(GenLauncherActiveColor) ||
        !string.IsNullOrWhiteSpace(GenLauncherDarkFillColor) ||
        !string.IsNullOrWhiteSpace(GenLauncherDarkBackGround) ||
        !string.IsNullOrWhiteSpace(GenLauncherLightBackGround) ||
        !string.IsNullOrWhiteSpace(GenLauncherDefaultTextColor) ||
        !string.IsNullOrWhiteSpace(GenLauncherDownloadTextColor) ||
        !string.IsNullOrWhiteSpace(GenLauncherListBoxSelectionColor1) ||
        !string.IsNullOrWhiteSpace(GenLauncherListBoxSelectionColor2) ||
        !string.IsNullOrWhiteSpace(GenLauncherButtonSelectionColor) ||
        HasBackgroundImage;

    /// <summary>
    ///     Gets a value indicating whether this theme publishes shell artwork, so the download cache and the presentation
    ///     layer agree on which versions have any.
    /// </summary>
    public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(GenLauncherBackgroundImageLink);
}
