using System;
using GenLauncherGO.Features.Settings;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Describes validated per-game paths and the complete canonical installation-set outcome.
/// </summary>
internal sealed record LauncherInstallationsValidationResult
{
    internal LauncherInstallationsValidationResult(
        GameInstallationValidationResult generalsValidation,
        GameInstallationValidationResult zeroHourValidation,
        LauncherInstallations canonicalInstallations,
        bool hasOverlappingPaths,
        bool isValid)
    {
        GeneralsValidation = generalsValidation ?? throw new ArgumentNullException(nameof(generalsValidation));
        ZeroHourValidation = zeroHourValidation ?? throw new ArgumentNullException(nameof(zeroHourValidation));
        CanonicalInstallations = canonicalInstallations ??
                                 throw new ArgumentNullException(nameof(canonicalInstallations));
        HasOverlappingPaths = hasOverlappingPaths;
        IsValid = isValid;
    }

    private GameInstallationValidationResult GeneralsValidation { get; }

    private GameInstallationValidationResult ZeroHourValidation { get; }

    public LauncherInstallations CanonicalInstallations { get; }

    public bool HasOverlappingPaths { get; }

    public bool IsValid { get; }

    public GameInstallationValidationResult GetValidation(SupportedGame game)
    {
        return PerGame.Select(game, GeneralsValidation, ZeroHourValidation);
    }

    /// <summary>
    ///     Gets the localization key that explains one game's outcome. Overlapping folders take precedence because they
    ///     make both selections unusable.
    /// </summary>
    public string GetStatusMessageKey(SupportedGame game)
    {
        if (HasOverlappingPaths)
        {
            return "OverlappingGameFolders";
        }

        GameInstallationValidationResult validation = GetValidation(game);
        return validation.Failure switch
        {
            GameInstallationValidationFailure.None => PerGame.Select(
                game,
                "ValidGeneralsInstallation",
                "ValidZeroHourInstallation"),
            GameInstallationValidationFailure.PathMissing => PerGame.Select(
                game,
                "ChooseGeneralsFolder",
                "ChooseZeroHourFolder"),
            GameInstallationValidationFailure.GameArchivesNotFound => PerGame.Select(
                game,
                "MissingGeneralsGameFiles",
                "MissingZeroHourGameFiles"),
            _ => "InstallationPathUnavailable"
        };
    }
}
