using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Startup;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Launching;

public sealed class GameFolderWriteAccessTests
{
    [Fact]
    public async Task EnsureWritableAsync_WhenFolderRefusesNewFiles_GrantsCurrentUserInheritableModifyAccessAsync()
    {
        using var directory = new TestDirectory();
        LauncherPaths paths = TestLauncherPaths.Create(directory);
        using var denial = new DeniedFileCreationScope(paths.GameDirectory);
        using var identity = WindowsIdentity.GetCurrent();
        ProcessStartInfo? grant = null;
        var access = new GameFolderWriteAccess(
            NullLogger<GameFolderWriteAccess>.Instance,
            (startInfo, _) =>
            {
                grant = startInfo;
                denial.Dispose();
                return Task.FromResult(0);
            });

        bool writable = await access.EnsureWritableAsync(paths, CancellationToken.None);

        writable.Should().BeTrue();
        grant.Should().NotBeNull();
        grant!.FileName.Should().Be(Path.Combine(Environment.SystemDirectory, "icacls.exe"));
        grant.UseShellExecute.Should().BeTrue();
        grant.Verb.Should().Be("runas");
        grant.ArgumentList.Should().Equal(
            paths.GameDirectory,
            "/grant",
            $"*{identity.User!.Value}:(OI)(CI)M",
            "/L");
    }

    [Fact]
    public async Task EnsureWritableAsync_WhenGameFolderIsReachedThroughReparsePoint_RejectsItWithoutRequestingAccessAsync()
    {
        using var directory = new TestDirectory();
        string target = directory.CreateDirectory("Target");
        string gameDirectory = directory.GetPath("Game");
        ReparsePointTestSupport.CreateDirectoryJunction(gameDirectory, target);
        bool requested = false;
        var access = new GameFolderWriteAccess(
            NullLogger<GameFolderWriteAccess>.Instance,
            (_, _) =>
            {
                requested = true;
                return Task.FromResult(0);
            });

        Func<Task> ensure = () => access.EnsureWritableAsync(
            TestLauncherPaths.Create(gameDirectory),
            CancellationToken.None);

        await ensure.Should().ThrowAsync<InvalidDataException>();
        requested.Should().BeFalse();
    }
}
