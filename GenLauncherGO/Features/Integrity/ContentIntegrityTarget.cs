using System;

namespace GenLauncherGO.Features.Integrity;

/// <summary>
///     Describes one launcher-owned directory that must be verified.
/// </summary>
internal sealed record ContentIntegrityTarget
{
    public ContentIntegrityTarget(
        string id,
        string displayName,
        string rootDirectory,
        ContentSourceKind sourceKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        Id = id;
        DisplayName = displayName;
        RootDirectory = rootDirectory;
        SourceKind = sourceKind;
    }

    /// <summary>
    ///     Gets the stable identifier used for snapshot persistence.
    /// </summary>
    public string Id { get; }

    public string DisplayName { get; }

    public string RootDirectory { get; }

    public ContentSourceKind SourceKind { get; init; }
}
