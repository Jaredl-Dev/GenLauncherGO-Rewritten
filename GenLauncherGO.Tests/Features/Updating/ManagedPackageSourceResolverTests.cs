using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Updating;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Updating;

public sealed class ManagedPackageSourceResolverTests
{
    [Fact]
    public async Task GetTotalBytesAsync_TransientMetadataFailure_RetriesOnNextLookupAsync()
    {
        Uri downloadUri = new("https://example.test/sample.gib");
        LauncherContentVersion version = TestLauncherContent.Version(
            simpleDownloadLink: downloadUri.AbsoluteUri);
        IDownloadFileMetadataReader metadataReader = Substitute.For<IDownloadFileMetadataReader>();
        metadataReader.ReadMetadataAsync(downloadUri, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<DownloadFileMetadata>(new HttpRequestException("Transient failure")),
                Task.FromResult(new DownloadFileMetadata(downloadUri, "sample.gib", 6)));
        ManagedPackageSourceResolver resolver = new(
            metadataReader,
            Substitute.For<IS3ObjectManifestReader>(),
            NullLogger<ManagedPackageSourceResolver>.Instance);

        long? first = await resolver.GetTotalBytesAsync(version, CancellationToken.None);
        long? second = await resolver.GetTotalBytesAsync(version, CancellationToken.None);

        first.Should().BeNull();
        second.Should().Be(6);
        await metadataReader.Received(2).ReadMetadataAsync(downloadUri, Arg.Any<CancellationToken>());
    }
}
