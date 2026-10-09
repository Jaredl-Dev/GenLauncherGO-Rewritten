using System;
using System.IO;

namespace GenLauncherGO.Shared.IO;

/// <summary>
///     Confirms that Windows lets the launcher create files in a directory.
/// </summary>
internal static class DirectoryWriteProbe
{
    /// <summary>
    ///     The HRESULT of <c>ERROR_WRITE_PROTECT</c>, raised by a locked SD card or USB drive or one made read-only by
    ///     policy.
    /// </summary>
    /// <remarks>
    ///     Kept deliberately: people run the portable archive from removable drives, and moving the launcher is their
    ///     fix. Tests cannot create write-protected media, so this case has no automated coverage.
    /// </remarks>
    private const int ErrorWriteProtectHResult = unchecked((int)0x80070013);

    /// <summary>
    ///     Creates and deletes a real file in an existing directory.
    /// </summary>
    /// <returns><see langword="false" /> when Windows denies creating the file.</returns>
    public static bool CanCreateFiles(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        try
        {
            // Only a real file proves access: permissions alone miss Controlled Folder Access, share rights,
            // inherited Deny entries, and write-protected media. Windows deletes the file on close.
            new FileStream(
                Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp"),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose).Dispose();
        }
        catch (Exception exception) when (IsWriteDenied(exception))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Reports whether a file-system failure means Windows refuses writes to the location.
    /// </summary>
    public static bool IsWriteDenied(Exception exception)
    {
        return exception is UnauthorizedAccessException or IOException { HResult: ErrorWriteProtectHResult };
    }
}
