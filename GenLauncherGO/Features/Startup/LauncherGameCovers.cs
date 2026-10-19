using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Owns the official box covers shown wherever the launcher lets the user pick or switch a game.
/// </summary>
/// <remarks>
///     Each cover is decoded once and kept for the session, because the game-choice screen, the main window's game
///     switcher and the settings window all show it. Decoding to a bounded width matters: the Generals artwork ships
///     1054 pixels wide, while no screen shows a cover wider than 188 pixels.
/// </remarks>
internal static class LauncherGameCovers
{
    // Twice the widest on-screen cover, so the artwork stays sharp at 200% display scaling.
    private const int DecodeWidth = 376;

    private static readonly Lazy<Bitmap> _generals =
        new(() => Load("avares://GenLauncherGO/Shared/Resources/Images/OfficialCoverGenerals.png"));

    private static readonly Lazy<Bitmap> _zeroHour =
        new(() => Load("avares://GenLauncherGO/Shared/Resources/Images/OfficialCoverZeroHour.jpg"));

    /// <summary>
    ///     Returns the cover of a supported game, decoding it on first use.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="game" /> is not supported.</exception>
    public static Bitmap Get(SupportedGame game)
    {
        return PerGame.Select(game, _generals, _zeroHour).Value;
    }

    private static Bitmap Load(string uri)
    {
        using Stream stream = AssetLoader.Open(new Uri(uri));
        return Bitmap.DecodeToWidth(stream, DecodeWidth);
    }
}
