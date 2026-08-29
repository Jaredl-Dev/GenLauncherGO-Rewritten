using System.Collections.Generic;
using System.IO;
using System.Linq;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Mods;

public sealed class FileSystemLocalLauncherContentServiceTests
{
    [Fact]
    public void FindInstalledVersions_DiscoversCanonicalModPatchAndAddonFolders()
    {
        using TestDirectory directory = new();
        LauncherPaths paths = TestLauncherPaths.Create(directory);
        LauncherContentKey[] contentKeys =
        [
            TestLauncherContent.Version("Sample").ContentKey,
            TestLauncherContent.Version("Patch", type: ModificationType.Patch, parentContentName: "Sample").ContentKey,
            TestLauncherContent.Version("Addon", type: ModificationType.Addon, parentContentName: "Sample").ContentKey
        ];
        foreach (LauncherContentKey contentKey in contentKeys)
        {
            string versionPath = LauncherContentPathResolver.ResolveVersionPath(paths, contentKey)!.FullPath;
            Directory.CreateDirectory(versionPath);
            File.WriteAllText(Path.Combine(versionPath, "content.gib"), "content");
        }

        FileSystemLocalLauncherContentService service = new(
            NullLogger<FileSystemLocalLauncherContentService>.Instance);

        IReadOnlyList<LauncherContentVersion> installedVersions = service.FindInstalledVersions(paths);

        installedVersions.Select(version => version.ContentKey)
            .Should().BeEquivalentTo(contentKeys);
    }
}
