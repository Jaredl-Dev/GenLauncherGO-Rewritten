using System.Collections.Generic;
using System.IO;
using GenLauncherGO.Features.Integrity;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Mods;

public sealed class LauncherLocalContentReconcilerTests
{
    [Fact]
    public void DiscardContent_ModWithDependentPatchesAndAddons_DeletesAllFoldersAndImagesAndPrunesCatalog()
    {
        using TestDirectory directory = new();
        LauncherPaths paths = TestLauncherPaths.Create(directory);

        string modDir = Path.Combine(paths.ModsDirectory, "Shockwave", "1.0");
        string patchDir = Path.Combine(paths.ModsDirectory, "Shockwave", LauncherFileSystemLayout.PatchesFolderName, "Patch1", "1.0");
        string addonDir = Path.Combine(paths.ModsDirectory, "Shockwave", LauncherFileSystemLayout.AddonsFolderName, "Addon1", "1.0");
        Directory.CreateDirectory(modDir);
        Directory.CreateDirectory(patchDir);
        Directory.CreateDirectory(addonDir);
        File.WriteAllText(Path.Combine(modDir, "mod.big"), "mod");
        File.WriteAllText(Path.Combine(patchDir, "patch.big"), "patch");
        File.WriteAllText(Path.Combine(addonDir, "addon.big"), "addon");

        string modImgDir = Path.Combine(paths.ImagesDirectory, "Shockwave");
        string patchImgDir = Path.Combine(paths.ImagesDirectory, "Patch1");
        string addonImgDir = Path.Combine(paths.ImagesDirectory, "Addon1");
        Directory.CreateDirectory(modImgDir);
        Directory.CreateDirectory(patchImgDir);
        Directory.CreateDirectory(addonImgDir);
        File.WriteAllText(Path.Combine(modImgDir, "img.png"), "img");
        File.WriteAllText(Path.Combine(patchImgDir, "img.png"), "img");
        File.WriteAllText(Path.Combine(addonImgDir, "img.png"), "img");

        LauncherData launcherData = new();
        LauncherContentVersion modVersion = TestLauncherContent.Version("Shockwave", "1.0", installed: true);
        LauncherContentVersion patchVersion = TestLauncherContent.Version("Patch1", "1.0", type: ModificationType.Patch, parentContentName: "Shockwave", installed: true);
        LauncherContentVersion addonVersion = TestLauncherContent.Version("Addon1", "1.0", type: ModificationType.Addon, parentContentName: "Shockwave", installed: true);
        launcherData.AddOrUpdate(modVersion);
        launcherData.AddOrUpdate(patchVersion);
        launcherData.AddOrUpdate(addonVersion);

        LauncherLocalContentReconciler reconciler = new(
            new FileSystemLocalLauncherContentService(NullLogger<FileSystemLocalLauncherContentService>.Instance),
            Substitute.For<IContentIntegrityService>(),
            NullLogger<LauncherLocalContentReconciler>.Instance);

        IReadOnlyList<LauncherContentKey> dependentKeys =
            reconciler.DiscardContent(launcherData, modVersion.ContentKey, paths);

        dependentKeys.Should().BeEquivalentTo([patchVersion.ContentKey.WithoutVersion(), addonVersion.ContentKey.WithoutVersion()]);
        Directory.Exists(Path.Combine(paths.ModsDirectory, "Shockwave")).Should().BeFalse();
        Directory.Exists(modImgDir).Should().BeFalse();
        Directory.Exists(patchImgDir).Should().BeFalse();
        Directory.Exists(addonImgDir).Should().BeFalse();
        launcherData.Modifications.Should().BeEmpty();
        launcherData.Patches.Should().BeEmpty();
        launcherData.Addons.Should().BeEmpty();
    }

    [Fact]
    public void DiscardContent_PatchNamedAfterMod_KeepsTheModsAddons()
    {
        using TestDirectory directory = new();
        LauncherPaths paths = TestLauncherPaths.Create(directory);
        string originalGame = LauncherContentKey.OriginalGame.Name;
        string addonDir = Path.Combine(paths.ModsDirectory, "ShockWave", LauncherFileSystemLayout.AddonsFolderName, "Maps", "1.0");
        string patchDir = Path.Combine(paths.ModsDirectory, originalGame, LauncherFileSystemLayout.PatchesFolderName, "ShockWave", "1.1");
        Directory.CreateDirectory(addonDir);
        Directory.CreateDirectory(patchDir);
        File.WriteAllText(Path.Combine(addonDir, "maps.big"), "addon");
        File.WriteAllText(Path.Combine(patchDir, "patch.big"), "patch");

        LauncherData launcherData = new();
        LauncherContentVersion addonVersion = TestLauncherContent.Version("Maps", "1.0", type: ModificationType.Addon, parentContentName: "ShockWave", installed: true);
        LauncherContentVersion patchVersion = TestLauncherContent.Version("ShockWave", "1.1", type: ModificationType.Patch, parentContentName: originalGame, installed: true);
        launcherData.AddOrUpdate(TestLauncherContent.Version("ShockWave", "1.0", installed: true));
        launcherData.AddOrUpdate(addonVersion);
        launcherData.AddOrUpdate(patchVersion);

        LauncherLocalContentReconciler reconciler = new(
            new FileSystemLocalLauncherContentService(NullLogger<FileSystemLocalLauncherContentService>.Instance),
            Substitute.For<IContentIntegrityService>(),
            NullLogger<LauncherLocalContentReconciler>.Instance);

        reconciler.DiscardContent(launcherData, patchVersion.ContentKey, paths);

        Directory.Exists(addonDir).Should().BeTrue();
        launcherData.Addons.Should().ContainSingle(addon => addon.ContentKey.HasName("Maps"));
    }
}
