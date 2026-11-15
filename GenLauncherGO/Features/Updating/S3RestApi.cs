using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace GenLauncherGO.Features.Updating;

/// <summary>
///     Addresses and signs requests to the S3-compatible package hosts with AWS Signature Version 4.
/// </summary>
/// <remarks>
///     Addresses are path-style (<c>host/bucket/key</c>) and signatures use the <c>us-east-1</c> region, which is what
///     the legacy catalog's MinIO server accepts. Signing follows the S3 Signature Version 4 specification for header
///     and query-string authentication. Its URI encoding escapes every byte except the RFC 3986 unreserved characters,
///     which is exactly what <see cref="Uri.EscapeDataString(string)" /> does. Every address is built from segments
///     escaped that way and a query already in canonical order, so a request sends exactly the path and query it signs.
/// </remarks>
internal static class S3RestApi
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Region = "us-east-1";
    private const string Service = "s3";
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string SignedRequestHeaders = "host;x-amz-content-sha256;x-amz-date";

    /// <summary>
    ///     Creates a signed request for one page of a recursive ListObjectsV2 listing. Keys come back URL-encoded so that
    ///     any key survives XML.
    /// </summary>
    public static HttpRequestMessage CreateListObjectsRequest(
        S3ObjectManifestRequest source,
        string? continuationToken,
        DateTimeOffset signingTime)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<KeyValuePair<string, string>> query =
        [
            new("encoding-type", "url"),
            new("list-type", "2"),
            new("prefix", source.Prefix)
        ];
        if (!string.IsNullOrEmpty(continuationToken))
        {
            query.Add(new("continuation-token", continuationToken));
        }

        return CreateSignedGetRequest(
            new Uri(GetBucketAddress(source), UriKind.Absolute),
            query,
            source.AccessKey,
            source.SecretKey,
            signingTime);
    }

    /// <summary>
    ///     Creates a time-limited download address that carries its own credentials. Only the host is signed, so the
    ///     downloader can add a range header to resume.
    /// </summary>
    public static Uri CreatePresignedGetUri(
        S3ObjectManifestRequest source,
        string objectKey,
        DateTimeOffset signingTime,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        Uri objectUri = new(
            GetBucketAddress(source) + string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString)),
            UriKind.Absolute);
        return Presign(objectUri, source.AccessKey, source.SecretKey, signingTime, lifetime);
    }

    /// <summary>
    ///     Adds query-string authentication to an object address that has no query of its own.
    /// </summary>
    internal static Uri Presign(
        Uri objectUri,
        string accessKey,
        string secretKey,
        DateTimeOffset signingTime,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(objectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        string timestamp = FormatTimestamp(signingTime);
        string scope = GetScope(signingTime);
        string canonicalQuery = FormatCanonicalQuery(
        [
            new("X-Amz-Algorithm", Algorithm),
            new("X-Amz-Credential", $"{accessKey}/{scope}"),
            new("X-Amz-Date", timestamp),
            new("X-Amz-Expires", ((long)lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
            new("X-Amz-SignedHeaders", "host")
        ]);
        string canonicalRequest = string.Join(
            '\n',
            "GET",
            objectUri.AbsolutePath,
            canonicalQuery,
            $"host:{objectUri.Authority}",
            string.Empty,
            "host",
            "UNSIGNED-PAYLOAD");
        string signature = Sign(secretKey, signingTime, timestamp, scope, canonicalRequest);

        return new Uri(
            $"{objectUri.GetLeftPart(UriPartial.Path)}?{canonicalQuery}&X-Amz-Signature={signature}",
            UriKind.Absolute);
    }

    /// <summary>
    ///     Creates a GET request with header authentication for a resource address and its query parameters.
    /// </summary>
    internal static HttpRequestMessage CreateSignedGetRequest(
        Uri resourceUri,
        IEnumerable<KeyValuePair<string, string>> query,
        string accessKey,
        string secretKey,
        DateTimeOffset signingTime)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        string timestamp = FormatTimestamp(signingTime);
        string scope = GetScope(signingTime);
        string canonicalQuery = FormatCanonicalQuery(query);
        string canonicalRequest = string.Join(
            '\n',
            "GET",
            resourceUri.AbsolutePath,
            canonicalQuery,
            $"host:{resourceUri.Authority}",
            $"x-amz-content-sha256:{EmptyPayloadHash}",
            $"x-amz-date:{timestamp}",
            string.Empty,
            SignedRequestHeaders,
            EmptyPayloadHash);
        string signature = Sign(secretKey, signingTime, timestamp, scope, canonicalRequest);

        HttpRequestMessage request = new(
            HttpMethod.Get,
            new Uri($"{resourceUri.GetLeftPart(UriPartial.Path)}?{canonicalQuery}", UriKind.Absolute));
        request.Headers.Add("x-amz-content-sha256", EmptyPayloadHash);
        request.Headers.Add("x-amz-date", timestamp);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            Algorithm,
            $"Credential={accessKey}/{scope},SignedHeaders={SignedRequestHeaders},Signature={signature}");
        return request;
    }

    /// <summary>
    ///     Gets the bucket address. An explicit scheme in the endpoint overrides the request's SSL preference, and port
    ///     443 implies HTTPS.
    /// </summary>
    private static string GetBucketAddress(S3ObjectManifestRequest source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.BucketName);

        string authority = source.Endpoint.Trim();
        bool useSsl = source.UseSsl;
        if (authority.Contains("://", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(authority, UriKind.Absolute, out Uri? endpointUri))
        {
            authority = endpointUri.Authority;
            useSsl = string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }
        else if (authority.EndsWith(":443", StringComparison.OrdinalIgnoreCase))
        {
            useSsl = true;
        }

        string scheme = useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        return $"{scheme}://{authority}/{Uri.EscapeDataString(source.BucketName)}/";
    }

    private static string Sign(
        string secretKey,
        DateTimeOffset signingTime,
        string timestamp,
        string scope,
        string canonicalRequest)
    {
        string canonicalRequestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
        string stringToSign = string.Join('\n', Algorithm, timestamp, scope, canonicalRequestHash);
        byte[] key = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("AWS4" + secretKey),
            Encoding.UTF8.GetBytes(FormatDate(signingTime)));
        key = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Region));
        key = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Service));
        key = HMACSHA256.HashData(key, "aws4_request"u8);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(stringToSign)));
    }

    private static string GetScope(DateTimeOffset signingTime)
    {
        return $"{FormatDate(signingTime)}/{Region}/{Service}/aws4_request";
    }

    private static string FormatDate(DateTimeOffset signingTime)
    {
        return signingTime.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    private static string FormatTimestamp(DateTimeOffset signingTime)
    {
        return signingTime.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    private static string FormatCanonicalQuery(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join(
            '&',
            parameters
                .Select(parameter => (
                    Name: Uri.EscapeDataString(parameter.Key),
                    Value: Uri.EscapeDataString(parameter.Value)))
                .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
                .ThenBy(parameter => parameter.Value, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Name}={parameter.Value}"));
    }
}
