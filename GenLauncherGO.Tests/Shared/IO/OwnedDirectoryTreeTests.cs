using System.IO;
using GenLauncherGO.Shared.IO;

namespace GenLauncherGO.Tests.Shared.IO;

public sealed class OwnedDirectoryTreeTests
{
    [Fact]
    public void PrepareEmpty_RemovesReadOnlyFiles()
    {
        using TestDirectory directory = new();
        string stagingPath = directory.CreateDirectory("Staging");
        string readOnlyFile = directory.CreateFile(Path.Combine("Staging", "Data", "GameData.ini"), "copied from a CD");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        OwnedDirectoryTree.PrepareEmpty(directory.Path, stagingPath);

        Directory.EnumerateFileSystemEntries(stagingPath).Should().BeEmpty();
    }
}
