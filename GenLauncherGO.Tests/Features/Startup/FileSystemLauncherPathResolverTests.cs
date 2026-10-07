using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
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

    /// <summary>
    ///     Denies the current user creating files directly in one directory. Windows enforces a Deny entry even for
    ///     an elevated process, so this reproduces a folder the launcher can see but not write.
    /// </summary>
    private sealed class DeniedFileCreationScope : IDisposable
    {
        private readonly DirectoryInfo _directory;
        private readonly FileSystemAccessRule _rule;

        public DeniedFileCreationScope(string directoryPath)
        {
            using var identity = WindowsIdentity.GetCurrent();
            _directory = new DirectoryInfo(directoryPath);
            _rule = new FileSystemAccessRule(
                identity.User ?? throw new InvalidOperationException("The current Windows user has no security identifier."),
                FileSystemRights.CreateFiles,
                AccessControlType.Deny);
            DirectorySecurity security = _directory.GetAccessControl();
            security.AddAccessRule(_rule);
            _directory.SetAccessControl(security);
        }

        public void Dispose()
        {
            DirectorySecurity security = _directory.GetAccessControl();
            security.RemoveAccessRuleSpecific(_rule);
            _directory.SetAccessControl(security);
        }
    }
}
