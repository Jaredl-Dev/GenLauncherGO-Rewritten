namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Describes how a manual import ended when it did not fail.
/// </summary>
internal enum ManualImportOutcome
{
    Installed,

    /// <summary>
    ///     The selection held no files to install.
    /// </summary>
    NothingToImport,

    /// <summary>
    ///     The version folder already holds files, so installing would have replaced them.
    /// </summary>
    VersionAlreadyInstalled
}
