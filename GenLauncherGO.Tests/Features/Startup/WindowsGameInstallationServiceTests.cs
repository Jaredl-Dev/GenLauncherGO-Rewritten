using System.Collections.Generic;
using System.IO;
using System.Linq;
using GenLauncherGO.Features.Startup;
using GenLauncherGO.Shared.IO;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Startup;

public sealed class WindowsGameInstallationServiceTests
{
    [Theory]
    [InlineData(SupportedGame.Generals, SupportedGame.ZeroHour)]
    [InlineData(SupportedGame.ZeroHour, SupportedGame.Generals)]
    public void Validate_WithOtherGamesArchives_RejectsFolder(object gameValue, object otherGameValue)
    {
        var game = (SupportedGame)gameValue;
        var otherGame = (SupportedGame)otherGameValue;
        using TestDirectory directory = new();
        string gameDirectory = CreateGameDirectory(directory, LauncherFileSystemLayout.GetGameArchiveNames(otherGame));
        string launcherDirectory = directory.CreateDirectory("Launcher");

        GameInstallationValidationResult result = CreateService().Validate(game, gameDirectory, launcherDirectory);

        result.Failure.Should().Be(GameInstallationValidationFailure.GameArchivesNotFound);
    }

    [Fact]
    public void Validate_WithOneArchiveMovedToDeploymentBackup_RemainsValid()
    {
        using TestDirectory directory = new();
        string gameDirectory = CreateGameDirectory(
            directory,
            LauncherFileSystemLayout.GetGameArchiveNames(SupportedGame.ZeroHour).Skip(1));
        string launcherDirectory = directory.CreateDirectory("Launcher");

        GameInstallationValidationResult result =
            CreateService().Validate(SupportedGame.ZeroHour, gameDirectory, launcherDirectory);

        result.IsValid.Should().BeTrue();
        result.CanonicalPath.Should().Be(PhysicalDirectoryPath.ResolveExisting(gameDirectory));
    }

    [Fact]
    public void FindContainingGame_InsideRetailZeroHourFolder_ReturnsZeroHour()
    {
        using TestDirectory directory = new();
        CreateGameDirectory(directory, LauncherFileSystemLayout.GetGameArchiveNames(SupportedGame.ZeroHour));
        string launcherDirectory = directory.CreateDirectory(Path.Combine("Game", "GenLauncherGO"));

        SupportedGame? containingGame = CreateService().FindContainingGame(launcherDirectory);

        containingGame.Should().Be(SupportedGame.ZeroHour);
    }

    private static string CreateGameDirectory(TestDirectory directory, IEnumerable<string> archiveNames)
    {
        string gameDirectory = directory.CreateDirectory("Game");
        foreach (string archiveName in archiveNames)
        {
            directory.CreateFile(Path.Combine("Game", archiveName), "archive");
        }

        return gameDirectory;
    }

    private static WindowsGameInstallationService CreateService()
    {
        return new WindowsGameInstallationService(
            Substitute.For<IGameInstallationRegistry>(),
            NullLogger<WindowsGameInstallationService>.Instance);
    }
}
