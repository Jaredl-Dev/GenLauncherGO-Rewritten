using System.IO;
using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Mods;

public sealed class FileSystemModificationThemeCacheTests
{
    /// <summary>
    ///     The cache is the only palette source while the catalog is offline, and a load failure is swallowed, so a
    ///     cache the launcher can no longer read silently drops every modification's theme. The YAML is exactly what
    ///     earlier versions wrote with the reflection serializer, including the computed properties it also emitted.
    /// </summary>
    [Fact]
    public void Load_ReadsPaletteCachedByEarlierVersions()
    {
        using var directory = new TestDirectory();
        LauncherPaths paths = TestLauncherPaths.Create(directory);
        string cachePath = ModificationImageCachePath.ResolveImagePath(
            paths,
            ModificationType.Mod,
            "Contra",
            ModificationImageCachePath.ResolvePaletteBaseName("009") + ".yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        File.WriteAllText(
            cachePath,
            """
            GenLauncherBorderColor: '#00e3ff'
            GenLauncherInactiveBorder: DarkGray
            GenLauncherInactiveBorder2: ''
            GenLauncherActiveColor: '#baff0c'
            GenLauncherDarkFillColor: ''
            GenLauncherDarkBackGround: ''
            GenLauncherLightBackGround: ''
            GenLauncherDefaultTextColor: White
            GenLauncherDownloadTextColor: ''
            GenLauncherListBoxSelectionColor1: '#E61d2057'
            GenLauncherListBoxSelectionColor2: ''
            GenLauncherButtonSelectionColor: ''
            GenLauncherBackgroundImageLink: https://cdn.example.test/contra.png
            HasValues: true
            HasBackgroundImage: true
            """);
        FileSystemModificationThemeCache cache = new(
            TestLauncherPaths.CreateRuntimePathContext(paths),
            new AtomicFileWriter(),
            NullLogger<YamlDocumentStore<LauncherContentTheme>>.Instance,
            NullLogger<FileSystemModificationThemeCache>.Instance);

        LauncherContentTheme? theme = cache.Load(new LauncherContentKey(ModificationType.Mod, string.Empty, "Contra", "009"));

        theme.Should().BeEquivalentTo(new LauncherContentTheme
        {
            GenLauncherBorderColor = "#00e3ff",
            GenLauncherInactiveBorder = "DarkGray",
            GenLauncherActiveColor = "#baff0c",
            GenLauncherDefaultTextColor = "White",
            GenLauncherListBoxSelectionColor1 = "#E61d2057",
            GenLauncherBackgroundImageLink = "https://cdn.example.test/contra.png"
        });
    }
}
