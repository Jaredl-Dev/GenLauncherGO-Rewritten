using System;
using System.IO;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Archives;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Mods;

public sealed class FileSystemManualModificationImporterTests
{
    [Fact]
    public void Import_ArchiveThatCannotBeExtracted_LeavesNoInstalledVersion()
    {
        using TestDirectory directory = new();
        LauncherPaths paths = TestLauncherPaths.Create(directory);
        string damagedArchive = directory.CreateFile(Path.Combine("Downloads", "Damaged.zip"), "not an archive");
        OwnedContentPath installedPath = ResolveModVersionPath(paths, "Damaged");

        Action act = () => CreateImporter().Import(
            [damagedArchive],
            PackageUpdatePathSet.CreateForManualImport(paths, installedPath));

        act.Should().Throw<Exception>();
        Directory.Exists(installedPath.FullPath).Should().BeFalse();
    }

    private static OwnedContentPath ResolveModVersionPath(LauncherPaths paths, string name)
    {
        return LauncherContentPathResolver.ResolveVersionPath(
            paths,
            new LauncherContentKey(ModificationType.Mod, string.Empty, name, "1.0"))!;
    }

    private static FileSystemManualModificationImporter CreateImporter()
    {
        return new FileSystemManualModificationImporter(
            new ArchiveExtractor(),
            NullLogger<FileSystemManualModificationImporter>.Instance);
    }
}
