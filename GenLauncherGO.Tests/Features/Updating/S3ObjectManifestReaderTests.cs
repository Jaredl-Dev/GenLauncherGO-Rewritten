using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Updating;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Updating;

public sealed class S3ObjectManifestReaderTests
{
    /// <summary>
    ///     A listing returns at most 1,000 keys per page, so a reader that stops at the first page installs part of a
    ///     package without any error. Keys arrive URL-encoded because the listing asks for <c>encoding-type=url</c>.
    /// </summary>
    [Fact]
    public async Task ReadManifestAsync_FollowsEveryPageAndDecodesEntriesAsync()
    {
        QueueHttpMessageHandler handler = new();
        handler.Enqueue(_ => CreateListingResponse(
            """
            <IsTruncated>true</IsTruncated>
            <NextContinuationToken>page-2/token=</NextContinuationToken>
            <Contents>
              <Key>ShockWave/Data/INI/Object/China+Infantry.ini</Key>
              <ETag>&quot;9e107d9d372bb6826bd81d3542a419d6&quot;</ETag>
              <Size>2048</Size>
            </Contents>
            <Contents>
              <Key>ShockWave/%21ShockWave.big</Key>
              <ETag>&quot;e4d909c290d0fb1ca068ffaddf22cbd0-12&quot;</ETag>
              <Size>734003200</Size>
            </Contents>
            """));
        handler.Enqueue(_ => CreateListingResponse(
            """
            <IsTruncated>false</IsTruncated>
            <Contents>
              <Key>ShockWave/Maps/Tournament+Desert/Tournament+Desert.map</Key>
              <ETag>&quot;d41d8cd98f00b204e9800998ecf8427e&quot;</ETag>
              <Size>512</Size>
            </Contents>
            """));
        using HttpClient httpClient = new(handler);
        S3ObjectManifestReader reader = new(httpClient, NullLogger<S3ObjectManifestReader>.Instance);

        IReadOnlyList<RemoteFileManifestEntry> files = await reader.ReadManifestAsync(
            new S3ObjectManifestRequest("gen.example.test:9000", "shockwave", "ShockWave", "access", "secret"),
            CancellationToken.None);

        files.Should().Equal(
            new RemoteFileManifestEntry("Data/INI/Object/China Infantry.ini", "9e107d9d372bb6826bd81d3542a419d6", 2048),
            new RemoteFileManifestEntry("!ShockWave.big", "e4d909c290d0fb1ca068ffaddf22cbd0-12", 734003200),
            new RemoteFileManifestEntry(
                "Maps/Tournament Desert/Tournament Desert.map",
                "d41d8cd98f00b204e9800998ecf8427e",
                512));
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].RequestUri!.Query.Should().Contain("continuation-token=page-2%2Ftoken%3D");
    }

    private static HttpResponseMessage CreateListingResponse(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"""
                 <?xml version="1.0" encoding="UTF-8"?>
                 <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
                 <Name>shockwave</Name>
                 <Prefix>ShockWave</Prefix>
                 <MaxKeys>1000</MaxKeys>
                 <EncodingType>url</EncodingType>
                 {body}
                 </ListBucketResult>
                 """,
                Encoding.UTF8,
                "application/xml")
        };
    }
}
