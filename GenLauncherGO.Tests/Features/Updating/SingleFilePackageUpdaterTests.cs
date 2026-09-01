using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Archives;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Updating;

public sealed class SingleFilePackageUpdaterTests
{
    [Fact]
    public async Task UpdateAsync_HardLinkedPartial_DoesNotWriteOutsideStagingAsync()
    {
        using TestDirectory directory = new();
        LauncherPaths launcherPaths = TestLauncherPaths.Create(directory);
        OwnedContentPath installedPath = LauncherContentPathResolver.ResolveVersionPath(
            launcherPaths,
            TestLauncherContent.Version("Sample").ContentKey)!;
        var paths = PackageUpdatePathSet.Create(launcherPaths, installedPath);
        DownloadFileMetadata metadata = new(
            new Uri("https://example.test/sample.gib"),
            "sample.gib",
            6,
            new EntityTagHeaderValue("\"v1\""));
        string stagedFile = await StagePartialAsync(metadata, paths, "abc");
        string outsideFile = Path.Combine(directory.Path, "outside.gib");
        await File.WriteAllTextAsync(outsideFile, "abc", TestContext.Current.CancellationToken);
        File.Delete(stagedFile);
        new WindowsHardLinkCreator().TryCreateHardLink(stagedFile, outsideFile).Should().BeTrue();
        QueueHttpMessageHandler handler = new();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("abcdef"))
        });
        using HttpClient httpClient = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        SingleFilePackageUpdater updater = new(
            new ResumableHttpFileDownloader(httpClient),
            new ArchiveExtractor(),
            NullLogger<SingleFilePackageUpdater>.Instance);

        await updater.UpdateAsync(
            metadata,
            paths,
            null,
            CancellationToken.None);

        (await File.ReadAllTextAsync(outsideFile, TestContext.Current.CancellationToken)).Should().Be("abc");
        (await File.ReadAllTextAsync(
            Path.Combine(paths.InstalledPath.FullPath, "sample.gib"),
            TestContext.Current.CancellationToken)).Should().Be("abcdef");
    }

    [Fact]
    public async Task UpdateAsync_ResumesStagedFileAndInstallsCompletePackageAsync()
    {
        using TestDirectory directory = new();
        LauncherPaths launcherPaths = TestLauncherPaths.Create(directory);
        LauncherContentKey contentKey = TestLauncherContent.Version("Sample").ContentKey;
        OwnedContentPath installedPath = LauncherContentPathResolver.ResolveVersionPath(launcherPaths, contentKey)!;
        var paths = PackageUpdatePathSet.Create(launcherPaths, installedPath);
        DownloadFileMetadata metadata = new(
            new Uri("https://example.test/sample.gib"),
            "sample.gib",
            6,
            new EntityTagHeaderValue("\"v1\""));
        await StagePartialAsync(metadata, paths, "abc");
        await File.WriteAllTextAsync(
            Path.Combine(paths.TemporaryPath.FullPath, "stale.txt"),
            "stale",
            TestContext.Current.CancellationToken);
        QueueHttpMessageHandler handler = new();
        handler.Enqueue(request =>
        {
            if (request.Headers.Range is null)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("abcdef"))
                };
            }

            HttpResponseMessage response = new(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("def"))
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(3, 5, 6);
            return response;
        });
        using HttpClient httpClient = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        ResumableHttpFileDownloader downloader = new(httpClient);
        SingleFilePackageUpdater updater = new(
            downloader,
            new ArchiveExtractor(),
            NullLogger<SingleFilePackageUpdater>.Instance);

        await updater.UpdateAsync(
            metadata,
            paths,
            null,
            CancellationToken.None);

        handler.RangeHeaders.Should().ContainSingle().Which.Should().Be("bytes=3-");
        handler.Requests.Single().Headers.IfRange?.ToString().Should().Be("\"v1\"");
        (await File.ReadAllTextAsync(
            Path.Combine(paths.InstalledPath.FullPath, "sample.gib"),
            TestContext.Current.CancellationToken)).Should().Be("abcdef");
        File.Exists(Path.Combine(paths.InstalledPath.FullPath, "stale.txt")).Should().BeFalse();
    }

    private static async Task<string> StagePartialAsync(
        DownloadFileMetadata metadata,
        PackageUpdatePathSet paths,
        string content)
    {
        RecordingFileDownloader downloader = new()
        {
            Handler = async (request, _) =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationFilePath)!);
                await File.WriteAllTextAsync(
                    request.DestinationFilePath,
                    content,
                    TestContext.Current.CancellationToken);
                throw new OperationCanceledException();
            }
        };
        SingleFilePackageUpdater updater = new(
            downloader,
            new ArchiveExtractor(),
            NullLogger<SingleFilePackageUpdater>.Instance);

        Func<Task> update = () => updater.UpdateAsync(metadata, paths, null, CancellationToken.None);

        await update.Should().ThrowAsync<OperationCanceledException>();
        return Directory.GetFiles(paths.TemporaryPath.FullPath).Single();
    }
}
