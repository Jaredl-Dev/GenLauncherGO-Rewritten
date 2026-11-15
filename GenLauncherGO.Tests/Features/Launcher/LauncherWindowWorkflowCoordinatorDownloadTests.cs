using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using GenLauncherGO.Features.Integrity;
using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Launcher.Views;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Features.Updating;
using GenLauncherGO.Shared.Dialogs;

namespace GenLauncherGO.Tests.Features.Launcher;

public sealed partial class LauncherWindowWorkflowCoordinatorTests
{
    [Fact]
    public void DeleteModificationAsyncForActiveModDownload_CancelsLifecycleAndRemovesPartialContent()
    {
        StaTestRunner.Run(async () =>
        {
            ILauncherDialogService dialogService = StubLauncherDialogService.AnsweringWarningConfirmations(true);
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherContentActionCoordinator coordinator = CreateContentActionCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog,
                packageDownloadService: downloadService);
            ModificationViewModel viewModel = CreateManagedShockwaveTile(packageActivityService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            Task download = UpdateModificationAsync(coordinator, fixture, viewModel);
            await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
            Task cancellation = DeleteModificationAsync(coordinator, fixture, viewModel);
            await downloadService.CancellationObserved.Task.WaitAsync(TestTimeouts.Wait);
            downloadService.Release();
            await Task.WhenAll(download, cancellation);

            catalog.UninstalledVersions.Should().ContainSingle().Which.Should().Match<LauncherContentKey>(contentKey =>
                contentKey.Name == "Shockwave" &&
                contentKey.Version == "1.0" &&
                contentKey.ContentType == ModificationType.Mod);
            catalog.DiscardedContents.Should().BeEmpty();
            catalog.LocalDataUpdateCount.Should().Be(1);
            catalog.SaveCount.Should().Be(1);
            fixture.ViewModel.ModsListSource.Should().ContainSingle()
                .Which.Should().BeSameAs(viewModel);
            viewModel.ContainerModification.Installed.Should().BeFalse();
            viewModel.IsSelected.Should().BeTrue();
            viewModel.VersionActionContent.Should().Be("Remove from list");
            await dialogService.Received(1).ShowWarningConfirmationAsync(
                Arg.Any<LauncherInfoDialogRequest>(),
                Arg.Any<string?>(),
                fixture.Owner);
        });
    }

    /// <summary>
    ///     Cancellation loses this race: the install already committed, so throwing the content away would delete a
    ///     version the user now has.
    /// </summary>
    [Fact]
    public void DeleteModificationAsyncWhenTheDownloadSucceedsFirst_KeepsTheInstalledVersion()
    {
        StaTestRunner.Run(async () =>
        {
            ILauncherDialogService dialogService = StubLauncherDialogService.AnsweringWarningConfirmations(true);
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherContentActionCoordinator coordinator = CreateContentActionCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog,
                packageDownloadService: downloadService);
            ModificationViewModel viewModel = CreateManagedShockwaveTile(packageActivityService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            Task download = UpdateModificationAsync(coordinator, fixture, viewModel);
            await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
            Task cancellation = DeleteModificationAsync(coordinator, fixture, viewModel);
            await downloadService.CancellationObserved.Task.WaitAsync(TestTimeouts.Wait);
            downloadService.Complete(PackageDownloadResult.Succeeded());
            await Task.WhenAll(download, cancellation);

            catalog.UninstalledVersions.Should().BeEmpty();
            catalog.DiscardedContents.Should().BeEmpty();
            viewModel.LatestVersion.Installation.Installed.Should().BeTrue();
            fixture.ViewModel.ModsListSource.Should().ContainSingle()
                .Which.Should().BeSameAs(viewModel);
        });
    }

    [Fact]
    public void DeleteModificationAsyncWhenDownloadCompletesBeforeConfirmation_PreservesInstalledVersion()
    {
        StaTestRunner.Run(async () =>
        {
            TaskCompletionSource<bool> confirmationEntered = new();
            Task? download = null;
            ControllablePackageDownloadService downloadService = new();
            ILauncherDialogService dialogService = Substitute.For<ILauncherDialogService>();
            dialogService.ShowWarningConfirmationAsync(
                    Arg.Any<LauncherInfoDialogRequest>(),
                    Arg.Any<string?>(),
                    Arg.Any<Avalonia.Controls.Window?>())
                .Returns(async _ =>
                {
                    confirmationEntered.TrySetResult(true);
                    downloadService.Complete(PackageDownloadResult.Succeeded());
                    if (download is not null)
                    {
                        await download;
                    }

                    return true;
                });
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            LauncherContentActionCoordinator coordinator = CreateContentActionCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog,
                packageDownloadService: downloadService);
            ModificationViewModel viewModel = CreateManagedShockwaveTile(packageActivityService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            download = UpdateModificationAsync(coordinator, fixture, viewModel);
            await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
            Task cancellation = DeleteModificationAsync(coordinator, fixture, viewModel);
            await confirmationEntered.Task.WaitAsync(TestTimeouts.Wait);
            await Task.WhenAll(download, cancellation);

            catalog.UninstalledVersions.Should().BeEmpty();
            viewModel.LatestVersion.Installation.Installed.Should().BeTrue();
            viewModel.ProgressMessage.Should().NotBe("Download canceled");
        });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void UpdateModificationAsyncForDeprecatedContent_OnlyDownloadsWhenConfirmed(
        bool confirmed,
        bool expectedUpdateButtonEnabled)
    {
        StaTestRunner.Run(async () =>
        {
            ILauncherDialogService dialogService = StubLauncherDialogService.AnsweringWarningConfirmations(confirmed);
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherContentActionCoordinator coordinator = CreateContentActionCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog,
                packageDownloadService: downloadService);
            ModificationViewModel viewModel = CreateTile(
                TestLauncherContent.From(TestLauncherContent.Version(
                    "Shockwave",
                    isSelected: true,
                    sourceKind: ContentSourceKind.ManagedSingleFile,
                    simpleDownloadLink: "https://example.test/package.zip",
                    deprecated: true)),
                packageActivityService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            Task download = UpdateModificationAsync(coordinator, fixture, viewModel);
            if (confirmed)
            {
                await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
                downloadService.Release();
            }

            await download.WaitAsync(TestTimeouts.Wait);

            downloadService.CallCount.Should().Be(confirmed ? 1 : 0);
            viewModel.LatestVersion.Installation.Installed.Should().Be(confirmed);
            viewModel.UpdateButtonEnabled.Should().Be(expectedUpdateButtonEnabled);
            await dialogService.Received(1).ShowWarningConfirmationAsync(
                Arg.Any<LauncherInfoDialogRequest>(),
                Arg.Any<string?>(),
                fixture.Owner);
        });
    }

    /// <summary>
    ///     Closing stops an in-flight download but must never discard it: the partial content is what a later session
    ///     resumes from, so uninstalling the version here would silently throw away the user's transfer.
    /// </summary>
    [Fact]
    public void PrepareForCloseAsyncSuspendsTheDownloadAnd_KeepsItsPartialContent()
    {
        StaTestRunner.Run(async () =>
        {
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherWindowWorkflowCoordinator coordinator = CreateCoordinator(
                packageActivityService,
                catalog: catalog);
            LauncherContentActionCoordinator contentActionCoordinator = CreateContentActionCoordinator(
                packageActivityService,
                catalog: catalog,
                packageDownloadService: downloadService);
            ModificationViewModel viewModel = CreateManagedShockwaveTile(packageActivityService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            Task download = UpdateModificationAsync(contentActionCoordinator, fixture, viewModel);
            await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
            Task closePreparation = coordinator.PrepareForCloseAsync();
            await downloadService.CancellationObserved.Task.WaitAsync(TestTimeouts.Wait);

            closePreparation.IsCompleted.Should().BeFalse();
            fixture.ViewModel.ModsListSource.Should().ContainSingle();

            downloadService.Release();
            await Task.WhenAll(download, closePreparation);

            catalog.UninstalledVersions.Should().BeEmpty();
            catalog.DiscardedContents.Should().BeEmpty();
            viewModel.LatestVersion.Installation.DownloadSuspended.Should().BeTrue();
            viewModel.VersionActionContent.Should().Be("Cancel Download");
            viewModel.IsVersionActionVisible.Should().BeTrue();
            fixture.ViewModel.ModsListSource.Should().ContainSingle()
                .Which.Should().BeSameAs(viewModel);
        });
    }

    [Fact]
    public void PrepareForCloseAsyncAndButtonCancellationRace_RunsRegisteredCleanupOnce()
    {
        StaTestRunner.Run(async () =>
        {
            ILauncherDialogService dialogService = StubLauncherDialogService.AnsweringWarningConfirmations(true);
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherWindowWorkflowCoordinator coordinator = CreateCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog);
            LauncherContentActionCoordinator contentActionCoordinator = CreateContentActionCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog,
                packageDownloadService: downloadService);
            ModificationViewModel viewModel = CreateManagedShockwaveTile(packageActivityService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            Task download = UpdateModificationAsync(contentActionCoordinator, fixture, viewModel);
            await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
            Task buttonCancellation = DeleteModificationAsync(contentActionCoordinator, fixture, viewModel);
            Task closePreparation = coordinator.PrepareForCloseAsync();
            await downloadService.CancellationObserved.Task.WaitAsync(TestTimeouts.Wait);

            buttonCancellation.IsCompleted.Should().BeFalse();
            closePreparation.IsCompleted.Should().BeFalse();

            downloadService.Release();
            await Task.WhenAll(download, buttonCancellation, closePreparation);

            catalog.UninstalledVersions.Should().ContainSingle();
            catalog.DiscardedContents.Should().BeEmpty();
            fixture.ViewModel.ModsListSource.Should().ContainSingle()
                .Which.Should().BeSameAs(viewModel);
        });
    }

    [Fact]
    public void DeleteModificationAsyncForSuspendedModDownload_CancelsSuspendedDownloadAndRemovesPartialContent()
    {
        StaTestRunner.Run(async () =>
        {
            ILauncherDialogService dialogService = StubLauncherDialogService.AnsweringWarningConfirmations(true);
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            LauncherContentActionCoordinator contentActionCoordinator = CreateContentActionCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog);
            ModificationViewModel viewModel = CreateManagedShockwaveTile(packageActivityService);
            viewModel.LatestVersion.Installation.DownloadSuspended = true;
            viewModel.LatestVersion.Installation.SuspendedProgressPercentage = 42;
            viewModel.RefreshFromModelAndPresentation();
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            viewModel.VersionActionContent.Should().Be("Cancel Download");
            viewModel.IsVersionActionVisible.Should().BeTrue();

            catalog.SaveHandler = () => viewModel.LatestVersion.Installation.DownloadSuspended.Should().BeFalse();
            await DeleteModificationAsync(contentActionCoordinator, fixture, viewModel);

            catalog.UninstalledVersions.Should().ContainSingle().Which.Should().Match<LauncherContentKey>(contentKey =>
                contentKey.Name == "Shockwave" &&
                contentKey.Version == "1.0" &&
                contentKey.ContentType == ModificationType.Mod);
            catalog.DiscardedContents.Should().BeEmpty();
            viewModel.LatestVersion.Installation.DownloadSuspended.Should().BeFalse();
            viewModel.VersionActionContent.Should().Be("Remove from list");
            fixture.ViewModel.ModsListSource.Should().ContainSingle()
                .Which.Should().BeSameAs(viewModel);
            await dialogService.Received(1).ShowWarningConfirmationAsync(
                Arg.Any<LauncherInfoDialogRequest>(),
                Arg.Any<string?>(),
                fixture.Owner);
        });
    }

    [Fact]
    public void DeleteModificationAsyncForSuspendedModDownloadDeclined_KeepsSuspendedDownload()
    {
        StaTestRunner.Run(async () =>
        {
            ILauncherDialogService dialogService = StubLauncherDialogService.AnsweringWarningConfirmations(false);
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherPackageActivityService packageActivityService = new();
            LauncherContentActionCoordinator contentActionCoordinator = CreateContentActionCoordinator(
                packageActivityService,
                dialogService,
                catalog: catalog);
            ModificationViewModel viewModel = CreateManagedShockwaveTile(packageActivityService);
            viewModel.LatestVersion.Installation.DownloadSuspended = true;
            viewModel.LatestVersion.Installation.SuspendedProgressPercentage = 42;
            viewModel.RefreshFromModelAndPresentation();
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.AddTile(viewModel);

            await DeleteModificationAsync(contentActionCoordinator, fixture, viewModel);

            catalog.UninstalledVersions.Should().BeEmpty();
            catalog.DiscardedContents.Should().BeEmpty();
            viewModel.LatestVersion.Installation.DownloadSuspended.Should().BeTrue();
            viewModel.VersionActionContent.Should().Be("Cancel Download");
            fixture.ViewModel.ModsListSource.Should().ContainSingle()
                .Which.Should().BeSameAs(viewModel);
            await dialogService.Received(1).ShowWarningConfirmationAsync(
                Arg.Any<LauncherInfoDialogRequest>(),
                Arg.Any<string?>(),
                fixture.Owner);
        });
    }

    /// <summary>
    ///     A paused download's progress and its Resume and Cancel actions are bound to its tile. A Mods folder refresh
    ///     that replaced the tile or stopped the download would make the next Resume start the download over.
    /// </summary>
    [Fact]
    public void ModsFolderRefreshDuringPausedDownload_KeepsTheDownloadPausedOnItsTile()
    {
        StaTestRunner.Run(async () =>
        {
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherContentVersion downloadingVersion = CreateVersion("Shockwave", ContentSourceKind.ManagedSingleFile);
            LauncherContentVersion copiedVersion = TestLauncherContent.Version("Copied", installed: true);
            catalog.Data.AddOrUpdate(downloadingVersion);
            catalog.LocalDataUpdateHandler = () => catalog.Data.AddOrUpdate(copiedVersion);
            ILocalLauncherContentService localContentService = Substitute.For<ILocalLauncherContentService>();
            localContentService.FindInstalledVersions(Arg.Any<LauncherPaths>())
                .Returns([downloadingVersion, copiedVersion]);
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherContentActionCoordinator contentActionCoordinator = CreateContentActionCoordinator(
                packageActivityService,
                catalog: catalog,
                packageDownloadService: downloadService);
            LauncherModsFolderChangeCoordinator modsFolderChangeCoordinator = CreateModsFolderChangeCoordinator(
                catalog,
                packageActivityService,
                localContentService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.ViewModel.Initialize(false);
            ModificationViewModel tile = fixture.ViewModel.ModsListSource.Single();
            using CancellationTokenSource windowLifetime = new();
            Task download = UpdateModificationAsync(contentActionCoordinator, fixture, tile);
            try
            {
                await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
                await UpdateModificationAsync(contentActionCoordinator, fixture, tile);
                fixture.Owner.Show();
                fixture.Owner.Activate();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

                await modsFolderChangeCoordinator.HandleLauncherActivatedAsync(fixture.Context, windowLifetime.Token);

                fixture.ViewModel.ModsListSource.Should().HaveCount(2)
                    .And.Contain(tile)
                    .And.Contain(listed => listed.ContainerModification.Name == "Copied");
                packageActivityService.IsDownloadPaused.Should().BeTrue();
                packageActivityService.GetActiveDownloadTask(tile).Should().NotBeNull();
            }
            finally
            {
                await windowLifetime.CancelAsync();
                downloadService.Release();
                await download.WaitAsync(TestTimeouts.Wait);
                fixture.Owner.Close();
            }
        });
    }

    /// <summary>
    ///     A paused add-on download's progress and its Resume and Cancel actions are bound to the add-on's tile. A Mods
    ///     folder refresh that listed a second tile for the add-on could show that one instead, and installing from it
    ///     would start the download over.
    /// </summary>
    [Fact]
    public void ModsFolderRefreshDuringPausedAddonDownload_KeepsTheDownloadOnTheAddonTile()
    {
        StaTestRunner.Run(async () =>
        {
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherContentVersion modVersion = CreateVersion("Shockwave", ContentSourceKind.ManagedSingleFile);
            LauncherContentVersion addonVersion = TestLauncherContent.Version(
                "Soundtrack",
                type: ModificationType.Addon,
                parentContentName: "Shockwave",
                sourceKind: ContentSourceKind.ManagedSingleFile,
                simpleDownloadLink: "https://example.test/soundtrack.zip");
            LauncherContentVersion copiedVersion = TestLauncherContent.Version("Copied", installed: true);
            catalog.Data.AddOrUpdate(modVersion);
            catalog.Data.AddOrUpdate(addonVersion);
            catalog.LocalDataUpdateHandler = () => catalog.Data.AddOrUpdate(copiedVersion);
            ILocalLauncherContentService localContentService = Substitute.For<ILocalLauncherContentService>();
            localContentService.FindInstalledVersions(Arg.Any<LauncherPaths>())
                .Returns([modVersion, copiedVersion]);
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherContentActionCoordinator contentActionCoordinator = CreateContentActionCoordinator(
                packageActivityService,
                catalog: catalog,
                packageDownloadService: downloadService);
            LauncherModsFolderChangeCoordinator modsFolderChangeCoordinator = CreateModsFolderChangeCoordinator(
                catalog,
                packageActivityService,
                localContentService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            fixture.ViewModel.Initialize(false);
            ModificationViewModel addonTile = fixture.ViewModel.AddonsListSource.Single();
            addonTile.IsSelected = true;
            using CancellationTokenSource windowLifetime = new();
            Task download = UpdateModificationAsync(contentActionCoordinator, fixture, addonTile);
            try
            {
                await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
                await UpdateModificationAsync(contentActionCoordinator, fixture, addonTile);
                fixture.Owner.Show();
                fixture.Owner.Activate();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

                await modsFolderChangeCoordinator.HandleLauncherActivatedAsync(fixture.Context, windowLifetime.Token);

                fixture.ViewModel.AddonsListSource.Should().ContainSingle().Which.Should().BeSameAs(addonTile);
                fixture.ViewModel.GetKnownContentTiles()
                    .Where(tile => tile.ContainerModification.ContentKey == addonTile.ContainerModification.ContentKey)
                    .Should().ContainSingle();
                packageActivityService.IsDownloadPaused.Should().BeTrue();
                packageActivityService.GetActiveDownloadTask(addonTile).Should().NotBeNull();
            }
            finally
            {
                await windowLifetime.CancelAsync();
                downloadService.Release();
                await download.WaitAsync(TestTimeouts.Wait);
                fixture.Owner.Close();
            }
        });
    }

    /// <summary>
    ///     The window binds each row's selection to its tile, and a newly selected mod reloads its patches and add-ons
    ///     with the controls disabled. A rebuilt row that fought the kept tile's old row over its selection would
    ///     repeat that reload without end, leaving the launcher stuck behind its loading overlay.
    /// </summary>
    [Fact]
    public void ModsFolderRefreshDuringPausedDownload_LeavesTheWindowSelectionSettled()
    {
        StaTestRunner.Run(async () =>
        {
            FakeLauncherContentCatalog catalog = CreateCatalog();
            LauncherContentVersion downloadingVersion = CreateVersion("Shockwave", ContentSourceKind.ManagedSingleFile);
            LauncherContentVersion copiedVersion = TestLauncherContent.Version("Copied", installed: true);
            catalog.Data.AddOrUpdate(downloadingVersion);
            catalog.ChildManifestReadHandler = (_, _) => Task.Delay(50);
            TaskCompletionSource refreshStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            catalog.LocalDataUpdateHandler = () =>
            {
                catalog.Data.AddOrUpdate(copiedVersion);
                refreshStarted.TrySetResult();
            };
            ILocalLauncherContentService localContentService = Substitute.For<ILocalLauncherContentService>();
            localContentService.FindInstalledVersions(Arg.Any<LauncherPaths>())
                .Returns([downloadingVersion, copiedVersion]);
            LauncherPackageActivityService packageActivityService = new();
            ControllablePackageDownloadService downloadService = new();
            LauncherContentActionCoordinator contentActionCoordinator = CreateContentActionCoordinator(
                packageActivityService,
                catalog: catalog,
                packageDownloadService: downloadService);
            WorkflowFixture fixture = new(catalog, packageActivityService);
            MainWindow window = CreateMainWindow(
                fixture.ViewModel,
                CreateCoordinator(packageActivityService, catalog: catalog),
                new ControlledUiExceptionBoundary(),
                contentActionCoordinator,
                CreateModsFolderChangeCoordinator(catalog, packageActivityService, localContentService));
            ModificationViewModel tile = fixture.ViewModel.ModsListSource.Single();
            LauncherWindowContext context = new(fixture.ViewModel, fixture.Content, window);
            Task download = contentActionCoordinator.UpdateModificationAsync(context, tile);
            try
            {
                await downloadService.Started.Task.WaitAsync(TestTimeouts.Wait);
                await contentActionCoordinator.UpdateModificationAsync(context, tile);
                window.Show();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                int readsBeforeRefresh = catalog.ChildManifestRequests.Count;

                window.Activate();
                await refreshStarted.Task.WaitAsync(TestTimeouts.Wait);
                await Task.Delay(TimeSpan.FromSeconds(1));

                fixture.ViewModel.ModsListSource.Should().HaveCount(2).And.Contain(tile);
                tile.IsSelected.Should().BeTrue();
                fixture.ViewModel.MainControlsEnabled.Should().BeTrue();
                catalog.ChildManifestRequests.Should().HaveCount(readsBeforeRefresh);
            }
            finally
            {
                downloadService.Release();
                await download.WaitAsync(TestTimeouts.Wait);
                window.Close();
                fixture.Owner.Close();
            }
        });
    }
}
