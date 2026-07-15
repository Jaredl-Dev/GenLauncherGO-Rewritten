using System;
using GenLauncherGO.Features.Mods;

namespace GenLauncherGO.Features.Updating;

/// <summary>
///     Provides S3-compatible catalog defaults used by legacy remote modification metadata.
/// </summary>
/// <remarks>
///     These values are retained for compatibility with the original GenLauncher backend. The original client
///     already shipped them in client-side code before this GenLauncherGO rewrite/fork, so this project treats them as
///     public legacy credentials rather than private application secrets. The "public key" is an S3 access key ID, not
///     a cryptographic public key; the paired secret must also be available to this client for direct S3 downloads.
///     Moving either value to client-side configuration or obfuscating it would not make the pair private.
///     The backend owner reports that this pair is read-only, without object edit permissions. That restriction is a
///     backend policy, not something the launcher verifies or enforces. Only the backend owner can change or rotate
///     the pair or its permissions; do not replace it with a privileged credential in this distributed client.
/// </remarks>
internal static class S3CatalogDefaults
{
    /// <summary>
    ///     Gets the legacy S3 access key ID used when catalog metadata does not provide one.
    /// </summary>
    public const string PublicAccessKey = "S58TYR9ISEZV8PBP8QG1";

    /// <summary>
    ///     Gets the legacy S3 secret access key used when catalog metadata does not provide one.
    /// </summary>
    public const string PublicSecretKey = "b2RU1oqVU5toJRnb4gODrXX8sBSgoLcHRX6qPWxj";

    /// <summary>
    ///     Creates a manifest request from one remote modification version.
    /// </summary>
    public static S3ObjectManifestRequest CreateManifestRequest(LauncherContentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new S3ObjectManifestRequest(
            version.S3HostLink,
            version.S3BucketName,
            version.S3FolderName,
            ResolveAccessKey(version),
            ResolveSecretKey(version));
    }

    /// <summary>
    ///     Resolves the access key for a modification version, falling back to the public catalog key.
    /// </summary>
    public static string ResolveAccessKey(LauncherContentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return string.IsNullOrEmpty(version.S3HostPublicKey)
            ? PublicAccessKey
            : version.S3HostPublicKey;
    }

    /// <summary>
    ///     Resolves the secret key for a modification version, falling back to the public catalog key.
    /// </summary>
    public static string ResolveSecretKey(LauncherContentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return string.IsNullOrEmpty(version.S3HostSecretKey)
            ? PublicSecretKey
            : version.S3HostSecretKey;
    }
}
