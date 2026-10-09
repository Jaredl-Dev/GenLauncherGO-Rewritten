namespace GenLauncherGO.Shared.IO;

/// <summary>
///     The outcome of trying to create a file in a directory.
/// </summary>
internal enum DirectoryWriteAccess
{
    Allowed,

    /// <summary>
    ///     Windows refused the file, which a permission grant can change.
    /// </summary>
    Denied,

    /// <summary>
    ///     The drive is write-protected, which no permission grant can change.
    /// </summary>
    WriteProtected
}
