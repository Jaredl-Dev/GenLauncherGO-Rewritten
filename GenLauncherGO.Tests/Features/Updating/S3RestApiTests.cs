using System;
using System.Net.Http;
using GenLauncherGO.Features.Updating;

namespace GenLauncherGO.Tests.Features.Updating;

/// <summary>
///     The expected signatures are the worked examples AWS publishes for S3 Signature Version 4, so a match proves the
///     signing follows the external contract rather than just agreeing with itself.
/// </summary>
public sealed class S3RestApiTests
{
    private const string AccessKey = "AKIAIOSFODNN7EXAMPLE";
    private const string SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private static readonly DateTimeOffset _signingTime = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Presign_MatchesAwsPublishedQueryStringExample()
    {
        Uri presigned = S3RestApi.Presign(
            new Uri("https://examplebucket.s3.amazonaws.com/test.txt"),
            AccessKey,
            SecretKey,
            _signingTime,
            TimeSpan.FromDays(1));

        presigned.AbsoluteUri.Should().Be(
            "https://examplebucket.s3.amazonaws.com/test.txt" +
            "?X-Amz-Algorithm=AWS4-HMAC-SHA256" +
            "&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request" +
            "&X-Amz-Date=20130524T000000Z" +
            "&X-Amz-Expires=86400" +
            "&X-Amz-SignedHeaders=host" +
            "&X-Amz-Signature=aeeed9bbccd4d02ee5c0109b86d86835f995330da4c265957d157751f604d404");
    }

    [Fact]
    public void CreateSignedGetRequest_MatchesAwsPublishedListObjectsExample()
    {
        using HttpRequestMessage request = S3RestApi.CreateSignedGetRequest(
            new Uri("https://examplebucket.s3.amazonaws.com/"),
            [new("max-keys", "2"), new("prefix", "J")],
            AccessKey,
            SecretKey,
            _signingTime);

        request.RequestUri!.AbsoluteUri.Should().Be("https://examplebucket.s3.amazonaws.com/?max-keys=2&prefix=J");
        request.Headers.Authorization!.ToString().Should().Be(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request," +
            "SignedHeaders=host;x-amz-content-sha256;x-amz-date," +
            "Signature=34b48302e7b5fa45bde8084f4b7868a86f0a534bc59db6670ed5711ef69dc6f7");
    }
}
