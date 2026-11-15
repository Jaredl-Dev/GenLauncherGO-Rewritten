using System;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Reports that an archive selected for a manual import could not be extracted.
/// </summary>
/// <remarks>
///     It lets the import workflow name the archive in a plain message rather than the generic failure dialog. It wraps
///     every failure during extraction, including one writing to staging such as a full disk, so the message passes the
///     cause on instead of guessing that the archive is at fault.
/// </remarks>
internal sealed class ManualImportArchiveException : Exception
{
    public ManualImportArchiveException(string archiveFileName, Exception innerException)
        : base($"The archive '{archiveFileName}' could not be extracted.", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveFileName);
        ArgumentNullException.ThrowIfNull(innerException);

        ArchiveFileName = archiveFileName;
    }

    public string ArchiveFileName { get; }
}
