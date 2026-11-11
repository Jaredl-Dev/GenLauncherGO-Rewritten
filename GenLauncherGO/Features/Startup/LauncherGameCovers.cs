using System;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Owns the official box covers shown wherever the launcher lets the user pick or switch a game.
/// </summary>
/// <remarks>
///     Each cover is decoded once and kept for the session, because the game-choice screen, the main window's game
///     switcher and the settings window all show it. No screen shows a cover wider than 188 pixels, so the artwork
///     ships no wider than 376 pixels, enough to stay sharp at 200% display scaling, and decodes at its own size.
/// </remarks>
internal static class LauncherGameCovers
{
    private static readonly Lazy<Bitmap> _generals =
        new(() => Load("avares://GenLauncherGO/Shared/Resources/Images/OfficialCoverGenerals.jpg"));

    private static readonly Lazy<Bitmap> _zeroHour =
        new(() => Load("avares://GenLauncherGO/Shared/Resources/Images/OfficialCoverZeroHour.jpg"));

    /// <summary>
    ///     Converts a game value to its cover, for markup that holds only the game.
    /// </summary>
    public static IValueConverter ForGame { get; } =
        new FuncValueConverter<SupportedGame, Bitmap?>(game => game == SupportedGame.Unknown ? null : Get(game));

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
        return new Bitmap(stream);
    }
}
