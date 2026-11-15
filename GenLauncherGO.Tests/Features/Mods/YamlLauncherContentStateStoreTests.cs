using System.IO;
using GenLauncherGO.Features.Integrity;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Mods;

public sealed class YamlLauncherContentStateStoreTests
{
    /// <summary>
    ///     A load failure is swallowed into empty state that the next save writes over the file, so the launcher would
    ///     silently forget installed versions, selections, and user-imported content. The YAML is the shape earlier
    ///     versions wrote with the reflection serializer.
    /// </summary>
    [Fact]
    public void Load_ReadsContentStateWrittenByEarlierVersions()
    {
        using var directory = new TestDirectory();
        LauncherPaths paths = TestLauncherPaths.Create(directory, SupportedGame.ZeroHour);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.LauncherDataFilePath)!);
        File.WriteAllText(
            paths.LauncherDataFilePath,
            """
            Addons:
            - ModificationType: Addon
              Name: HD Textures
              DependenceName: ShockWave
              Installed: true
              IsSelected: true
              NumberInList: 0
              ModificationVersions:
              - ModificationType: Addon
                Name: HD Textures
                Version: 1.1
                DependenceName: ShockWave
                Installed: true
                IsSelected: true
                DownloadSuspended: false
                SuspendedProgressPercentage: 0
                ContentSourceKind: ManagedSingleFile
            Modifications:
            - ModificationType: Mod
              Name: ShockWave
              DependenceName: ''
              Installed: true
              IsSelected: true
              NumberInList: 2
              ModificationVersions:
              - ModificationType: Mod
                Name: ShockWave
                Version: 1.2
                DependenceName: ''
                Installed: true
                IsSelected: true
                DownloadSuspended: false
                SuspendedProgressPercentage: 0
                ContentSourceKind: ManagedS3
              - ModificationType: Mod
                Name: ShockWave
                Version: 1.3
                DependenceName: ''
                Installed: false
                IsSelected: false
                DownloadSuspended: true
                SuspendedProgressPercentage: 37.5
                ContentSourceKind: ManagedS3
            Patches:
            - ModificationType: Patch
              Name: Balance Patch
              DependenceName: ShockWave
              Installed: true
              IsSelected: false
              NumberInList: 0
              ModificationVersions:
              - ModificationType: Patch
                Name: Balance Patch
                Version: 0.9
                DependenceName: ShockWave
                Installed: true
                IsSelected: false
                DownloadSuspended: false
                SuspendedProgressPercentage: 0
                ContentSourceKind: Manual
            """);
        YamlLauncherContentStateStore store = new(
            new AtomicFileWriter(),
            NullLogger<YamlDocumentStore<LauncherContentState>>.Instance);

        LauncherContentState state = store.Load(paths);

        state.Should().BeEquivalentTo(new LauncherContentState
        {
            Addons =
            [
                new LauncherContentEntryState
                {
                    ModificationType = ModificationType.Addon,
                    Name = "HD Textures",
                    DependenceName = "ShockWave",
                    Installed = true,
                    IsSelected = true,
                    ModificationVersions =
                    [
                        new LauncherContentVersionState
                        {
                            ModificationType = ModificationType.Addon,
                            Name = "HD Textures",
                            Version = "1.1",
                            DependenceName = "ShockWave",
                            Installed = true,
                            IsSelected = true,
                            ContentSourceKind = ContentSourceKind.ManagedSingleFile
                        }
                    ]
                }
            ],
            Modifications =
            [
                new LauncherContentEntryState
                {
                    ModificationType = ModificationType.Mod,
                    Name = "ShockWave",
                    Installed = true,
                    IsSelected = true,
                    NumberInList = 2,
                    ModificationVersions =
                    [
                        new LauncherContentVersionState
                        {
                            ModificationType = ModificationType.Mod,
                            Name = "ShockWave",
                            Version = "1.2",
                            Installed = true,
                            IsSelected = true,
                            ContentSourceKind = ContentSourceKind.ManagedS3
                        },
                        new LauncherContentVersionState
                        {
                            ModificationType = ModificationType.Mod,
                            Name = "ShockWave",
                            Version = "1.3",
                            DownloadSuspended = true,
                            SuspendedProgressPercentage = 37.5,
                            ContentSourceKind = ContentSourceKind.ManagedS3
                        }
                    ]
                }
            ],
            Patches =
            [
                new LauncherContentEntryState
                {
                    ModificationType = ModificationType.Patch,
                    Name = "Balance Patch",
                    DependenceName = "ShockWave",
                    Installed = true,
                    ModificationVersions =
                    [
                        new LauncherContentVersionState
                        {
                            ModificationType = ModificationType.Patch,
                            Name = "Balance Patch",
                            Version = "0.9",
                            DependenceName = "ShockWave",
                            Installed = true,
                            ContentSourceKind = ContentSourceKind.Manual
                        }
                    ]
                }
            ]
        });
    }
}
