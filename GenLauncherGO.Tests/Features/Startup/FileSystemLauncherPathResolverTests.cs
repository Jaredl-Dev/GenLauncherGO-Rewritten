using System.IO;
using GenLauncherGO.Features.Startup;

namespace GenLauncherGO.Tests.Features.Startup;

public sealed class FileSystemLauncherPathResolverTests
{
    [Fact]
    public void TryPrepareLauncherDirectories_WhenDataFolderRefusesNewFiles_ReturnsFalse()
    {
        using var directory = new TestDirectory();
        var paths = new LauncherStoragePaths(directory.Path);
        Directory.CreateDirectory(paths.DataDirectory);
        using var denial = new DeniedFileCreationScope(paths.DataDirectory);
        var resolver = new FileSystemLauncherPathResolver();

        bool prepared = resolver.TryPrepareLauncherDirectories(paths);

        prepared.Should().BeFalse();
    }
}
