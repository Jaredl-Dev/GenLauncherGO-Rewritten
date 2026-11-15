using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using GenLauncherGO.Features.Integrity;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Archives;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Mods;

public sealed class LauncherManualImportCoordinatorTests
{
    /// <summary>
    ///     An import that matches a published version has to be compared with that package at its first launch.
    ///     Trusting it at import would accept modified files as the official package and never flag them.
    /// </summary>
    [Fact]
    public void ImportAsync_MatchingRepositoryVersion_StaysUntrackedUntilItsFirstLaunch()
    {
        StaTestRunner.Run(async () =>
        {
            using TestDirectory directory = new();
            LauncherPaths paths = TestLauncherPaths.Create(directory);
            LauncherContentVersion publishedVersion = TestLauncherContent.S3Version();
            FakeLauncherContentCatalog catalog = new();
            catalog.Data.AddOrUpdate(publishedVersion);
            FileSystemLaunchContentIntegrityResolutionService resolutionService = CreateResolutionService();
            LauncherManualImportCoordinator coordinator = CreateCoordinator(
                new StubLauncherFilePicker
                {
                    ManualPackageFilesResult = [directory.CreateFile("notes.txt", "not the published package")]
                },
                CreateDialogService(
                    importFolder: false,
                    importDetails: new ManualModificationDialogResult(publishedVersion.Name, publishedVersion.Version)),
                paths,
                catalog,
                new FileSystemManualModificationImporter(
                    new ArchiveExtractor(),
                    NullLogger<FileSystemManualModificationImporter>.Instance));

            await coordinator.ImportAsync(ModificationType.Mod, new Window(), null, CancellationToken.None);

            LaunchContentIntegrityVerificationResult verification =
                await resolutionService.VerifyAsync(paths, [publishedVersion], CancellationToken.None);
            verification.Report.Issues.Should().ContainSingle(issue => issue.Kind == IntegrityIssueKind.Untracked);
        });
    }

    /// <summary>
    ///     An import is staged inside the launcher's data, so importing a folder that holds that data copies staging into
    ///     itself until the stack overflows. A junction above the selected folder hides this from its path text.
    /// </summary>
    [Fact]
    public void ImportAsync_FolderHoldingLauncherData_IsRefusedBeforeImporting()
    {
        StaTestRunner.Run(async () =>
        {
            using TestDirectory directory = new();
            LauncherPaths paths = TestLauncherPaths.Create(directory);
            string launcherDirectory = directory.GetPath("Launcher");
            string launcherAlias = directory.GetPath("LauncherAlias");
            ReparsePointTestSupport.CreateDirectoryJunction(launcherAlias, launcherDirectory);
            ILauncherDialogService dialogService = CreateDialogService(
                importFolder: true,
                importDetails: new ManualModificationDialogResult("Loop", "1"));
            IManualModificationImporter importer = Substitute.For<IManualModificationImporter>();
            LauncherManualImportCoordinator coordinator = CreateCoordinator(
                new StubLauncherFilePicker
                {
                    ManualContentFolderResult = Path.Combine(
                        launcherAlias,
                        Path.GetRelativePath(launcherDirectory, paths.OwnedGameDataDirectory))
                },
                dialogService,
                paths,
                importer: importer);

            LauncherContent? imported = await coordinator.ImportAsync(
                ModificationType.Mod,
                new Window(),
                null,
                CancellationToken.None);

            imported.Should().BeNull();
            importer.DidNotReceiveWithAnyArgs().Import(default!, default!);
            await dialogService.Received(1).ShowErrorAsync(
                Arg.Is<LauncherInfoDialogRequest>(request =>
                    request.DetailMessage == new FakeStringLocalizer()["ManualImportFolderContainsLauncherData"]),
                Arg.Any<Window?>());
        });
    }

    /// <summary>
    ///     A mod with the original game's name shares its folder, so deleting that mod would delete the game's patches.
    /// </summary>
    [Fact]
    public void ImportAsync_ModNamedOriginalGame_IsRefusedInTheImportDetails()
    {
        StaTestRunner.Run(async () =>
        {
            using TestDirectory directory = new();
            Func<string, string?> getNameRefusal = _ => null;
            LauncherManualImportCoordinator coordinator = CreateCoordinator(
                new StubLauncherFilePicker { ManualPackageFilesResult = [directory.CreateFile("Mod.zip", "archive")] },
                CreateDialogService(importFolder: false, importDetails: null, check => getNameRefusal = check),
                TestLauncherPaths.Create(directory));

            await coordinator.ImportAsync(ModificationType.Mod, new Window(), null, CancellationToken.None);

            getNameRefusal(LauncherContentKey.OriginalGame.Name.ToLowerInvariant()).Should().NotBeNull();
        });
    }

    /// <summary>
    ///     Answers the import source prompt and the import details dialog, and hands the details dialog's name check
    ///     to <paramref name="captureNameRefusal" />.
    /// </summary>
    private static ILauncherDialogService CreateDialogService(
        bool importFolder,
        ManualModificationDialogResult? importDetails,
        Action<Func<string, string?>>? captureNameRefusal = null)
    {
        ILauncherDialogService dialogService = Substitute.For<ILauncherDialogService>();
        dialogService.ShowInfoChoiceAsync(
                Arg.Any<LauncherInfoDialogRequest>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Window?>())
            .Returns((bool?)importFolder);
        dialogService.ShowManualModificationImportAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Do<Func<string, string?>>(check => captureNameRefusal?.Invoke(check)),
                Arg.Any<Func<string, string, bool>>(),
                Arg.Any<Window?>())
            .Returns(importDetails);
        return dialogService;
    }

    private static LauncherManualImportCoordinator CreateCoordinator(
        StubLauncherFilePicker filePicker,
        ILauncherDialogService dialogService,
        LauncherPaths paths,
        FakeLauncherContentCatalog? catalog = null,
        IManualModificationImporter? importer = null)
    {
        return new LauncherManualImportCoordinator(
            filePicker,
            dialogService,
            catalog ?? new FakeLauncherContentCatalog(),
            TestLauncherPaths.CreateRuntimePathContext(paths),
            importer ?? Substitute.For<IManualModificationImporter>(),
            new FakeStringLocalizer(),
            NullLogger<LauncherManualImportCoordinator>.Instance);
    }

    private static FileSystemLaunchContentIntegrityResolutionService CreateResolutionService()
    {
        return new FileSystemLaunchContentIntegrityResolutionService(
            new FileSystemContentIntegrityService(
                new AtomicFileWriter(),
                NullLogger<FileSystemContentIntegrityService>.Instance),
            new ManagedPackageSourceResolver(
                Substitute.For<IDownloadFileMetadataReader>(),
                Substitute.For<IS3ObjectManifestReader>(),
                NullLogger<ManagedPackageSourceResolver>.Instance),
            Substitute.For<IS3PackageUpdater>(),
            Substitute.For<ISingleFilePackageUpdater>(),
            Substitute.For<IFileHashService>(),
            NullLogger<FileSystemLaunchContentIntegrityResolutionService>.Instance);
    }
}
