using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using GenLauncherGO.Shared.Remote;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Features.Updating;

/// <summary>
///     Reads S3-compatible object listings with signed ListObjectsV2 requests.
/// </summary>
internal sealed class S3ObjectManifestReader : IS3ObjectManifestReader
{
    private const string NotAListingMessage = "The S3 host did not return an object listing.";

    private static readonly HttpClient _sharedHttpClient = SharedHttpClientFactory.Create(TimeSpan.FromSeconds(60));

    private static readonly XNamespace _s3Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    private readonly HttpClient _httpClient;

    private readonly ILogger<S3ObjectManifestReader> _logger;

    public S3ObjectManifestReader(ILogger<S3ObjectManifestReader> logger)
        : this(_sharedHttpClient, logger)
    {
    }

    internal S3ObjectManifestReader(HttpClient httpClient, ILogger<S3ObjectManifestReader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Reads an authenticated S3-compatible object listing, returning manifest entries with prefix-relative names.
    /// </summary>
    /// <remarks>
    ///     A listing returns at most 1,000 keys per page, so pages are followed until the host stops returning a
    ///     continuation token; stopping early would install part of a package.
    /// </remarks>
    public async Task<IReadOnlyList<RemoteFileManifestEntry>> ReadManifestAsync(
        S3ObjectManifestRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // S3RestApi validates the endpoint, bucket and keys before the first request. It accepts any prefix, and an
        // empty one would list the whole bucket as this package.
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prefix);

        List<RemoteFileManifestEntry> files = [];
        string? continuationToken = null;
        do
        {
            XElement page = await ReadPageAsync(request, continuationToken, cancellationToken).ConfigureAwait(false);
            foreach (XElement contents in page.Elements(_s3Namespace + "Contents"))
            {
                files.Add(ToManifestEntry(contents, request.Prefix));
            }

            continuationToken = page.Element(_s3Namespace + "NextContinuationToken")?.Value;
        }
        while (!string.IsNullOrEmpty(continuationToken));

        _logger.LogDebug(
            "Read {FileCount} S3 manifest entries from bucket {BucketName}, prefix {Prefix}.",
            files.Count,
            request.BucketName,
            request.Prefix);
        return files;
    }

    private async Task<XElement> ReadPageAsync(
        S3ObjectManifestRequest request,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = S3RestApi.CreateListObjectsRequest(
            request,
            continuationToken,
            DateTimeOffset.UtcNow);
        using HttpResponseMessage response = await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        XDocument document;
        try
        {
            document = await XDocument.LoadAsync(content, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        }
        // A proxy or captive portal can answer with a page that is not XML at all.
        catch (XmlException exception)
        {
            throw new InvalidDataException(NotAListingMessage, exception);
        }

        // A well-formed reply that is not a listing would otherwise read as an empty package.
        if (document.Root is not { } root || root.Name != _s3Namespace + "ListBucketResult")
        {
            throw new InvalidDataException(NotAListingMessage);
        }

        return root;
    }

    private static RemoteFileManifestEntry ToManifestEntry(XElement contents, string prefix)
    {
        string? encodedKey = contents.Element(_s3Namespace + "Key")?.Value;
        string? size = contents.Element(_s3Namespace + "Size")?.Value;
        if (string.IsNullOrEmpty(encodedKey) ||
            !ulong.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out ulong sizeInBytes))
        {
            throw new InvalidDataException("The S3 listing contained an entry without a key or size.");
        }

        return new RemoteFileManifestEntry(
            StripPrefix(WebUtility.UrlDecode(encodedKey), prefix),
            NormalizeETag(contents.Element(_s3Namespace + "ETag")?.Value ?? string.Empty),
            sizeInBytes);
    }

    private static string StripPrefix(string key, string prefix)
    {
        string normalizedPrefix = prefix.TrimEnd('/') + "/";
        if (key.StartsWith(normalizedPrefix, StringComparison.Ordinal))
        {
            return key[normalizedPrefix.Length..];
        }

        return key;
    }

    private static string NormalizeETag(string eTag)
    {
        return eTag.Trim().Trim('"');
    }
}
