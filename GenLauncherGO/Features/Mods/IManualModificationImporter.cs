using System.Collections.Generic;
using System.Threading;
using GenLauncherGO.Features.Updating;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Imports user-selected modification content into a launcher-managed content folder.
/// </summary>
internal interface IManualModificationImporter
{
    /// <summary>
    ///     Imports the source files and folders into the installed version folder.
    /// </summary>
    /// <param name="sourcePaths">
    ///     The selected files and folders. A folder contributes its contents, not the folder itself.
    /// </param>
    /// <param name="paths">The staging, installed, and recovery-backup folders for the imported version.</param>
    /// <param name="cancellationToken">The token used to cancel import work.</param>
    /// <returns>Whether the content was installed, or why it was not.</returns>
    ManualImportOutcome Import(
        IReadOnlyList<string> sourcePaths,
        PackageUpdatePathSet paths,
        CancellationToken cancellationToken = default);
}
