namespace GenLauncherGO.Features.Updating;

/// <summary>
///     Describes the single, terminal outcome of a launcher package download.
/// </summary>
internal sealed record PackageDownloadResult
{
    private PackageDownloadResult(
        PackageDownloadStatus status,
        string messageKey)
    {
        Status = status;
        MessageKey = messageKey;
    }

    public PackageDownloadStatus Status { get; }

    /// <summary>
    ///     Gets the localization key of the message that explains a recoverable failure, or an empty string for any
    ///     other outcome.
    /// </summary>
    public string MessageKey { get; }

    public static PackageDownloadResult Succeeded()
    {
        return new PackageDownloadResult(
            PackageDownloadStatus.Succeeded,
            string.Empty);
    }

    public static PackageDownloadResult Canceled()
    {
        return new PackageDownloadResult(
            PackageDownloadStatus.Canceled,
            string.Empty);
    }

    /// <summary>
    ///     Creates a result for a download the launcher stopped to shut down, keeping its partial content to resume.
    /// </summary>
    public static PackageDownloadResult Suspended()
    {
        return new PackageDownloadResult(
            PackageDownloadStatus.Suspended,
            string.Empty);
    }

    /// <summary>
    ///     Creates an expected failure result that can normally be retried or corrected by the user.
    /// </summary>
    public static PackageDownloadResult RecoverableFailure(string messageKey)
    {
        return new PackageDownloadResult(
            PackageDownloadStatus.RecoverableFailure,
            messageKey);
    }

    /// <summary>
    ///     Creates a result for a failure the launcher did not anticipate, whose details are only in the log.
    /// </summary>
    public static PackageDownloadResult UnexpectedFailure()
    {
        return new PackageDownloadResult(
            PackageDownloadStatus.UnexpectedFailure,
            string.Empty);
    }
}
