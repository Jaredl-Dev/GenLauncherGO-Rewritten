using System;
using GenLauncherGO.Shared.Localization;

namespace GenLauncherGO.Shared.Dialogs;

internal sealed class LauncherInfoDialogRequest(
    string mainMessage,
    string detailMessage,
    double? detailFontSize = null,
    string? cancelText = null,
    string? linkText = null,
    string? linkUri = null)
{
    public string MainMessage { get; } = mainMessage ?? throw new ArgumentNullException(nameof(mainMessage));

    public string DetailMessage { get; } = detailMessage ?? throw new ArgumentNullException(nameof(detailMessage));

    public double? DetailFontSize { get; } = detailFontSize;

    public string? CancelText { get; } = cancelText;

    /// <summary>
    ///     Gets the text of an optional web link that opens <see cref="LinkUri" /> without closing the dialog.
    /// </summary>
    public string? LinkText { get; } = linkText;

    public string? LinkUri { get; } = (linkText is null) == (linkUri is null)
        ? linkUri
        : throw new ArgumentException("A dialog link needs both its text and its URI.", nameof(linkUri));

    /// <summary>
    ///     Creates the canonical message shown when a user-requested settings change could not be persisted.
    /// </summary>
    public static LauncherInfoDialogRequest CreateSettingsSaveFailure(
        ILauncherStringLocalizer stringLocalizer)
    {
        ArgumentNullException.ThrowIfNull(stringLocalizer);
        return new LauncherInfoDialogRequest(
            stringLocalizer["SettingsSaveFailedTitle"],
            stringLocalizer["SettingsSaveFailedDetails"]);
    }
}
