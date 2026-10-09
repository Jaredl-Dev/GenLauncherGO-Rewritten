using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace GenLauncherGO.Tests.Testing;

/// <summary>
///     Denies the current user creating files directly in one directory. Windows enforces a Deny entry even for an
///     elevated process, so this reproduces a folder the launcher can see but not write.
/// </summary>
/// <remarks>
///     Disposing lifts the denial and may be repeated, so a test can lift it early to stand in for Windows granting
///     access.
/// </remarks>
internal sealed class DeniedFileCreationScope : IDisposable
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
