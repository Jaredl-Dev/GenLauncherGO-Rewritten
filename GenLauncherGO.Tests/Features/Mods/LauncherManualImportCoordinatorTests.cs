using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Dialogs;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Mods;

public sealed class LauncherManualImportCoordinatorTests
{
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
    ///     Answers the import source prompt and the import details dialog.
    /// </summary>
    private static ILauncherDialogService CreateDialogService(
        bool importFolder,
        ManualModificationDialogResult? importDetails)
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
                Arg.Any<Window?>())
            .Returns(importDetails);
        return dialogService;
    }

    private static LauncherManualImportCoordinator CreateCoordinator(
        StubLauncherFilePicker filePicker,
        ILauncherDialogService dialogService,
        LauncherPaths paths,
        IManualModificationImporter? importer = null)
    {
        return new LauncherManualImportCoordinator(
            filePicker,
            dialogService,
            new FakeLauncherContentCatalog(),
            TestLauncherPaths.CreateRuntimePathContext(paths),
            importer ?? Substitute.For<IManualModificationImporter>(),
            TestLaunchContentIntegrityCoordinator.Create(dialogService: dialogService),
            new FakeStringLocalizer(),
            NullLogger<LauncherManualImportCoordinator>.Instance);
    }
}
