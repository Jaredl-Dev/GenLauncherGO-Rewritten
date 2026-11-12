namespace GenLauncherGO.Features.Mods;

internal enum InfoDialogKind
{
    Info,

    InfoAction,

    Error,

    WarningConfirmation,

    /// <summary>
    ///     Offers a primary and a secondary action beside a cancel that dismisses the dialog with neither.
    /// </summary>
    Choice
}
