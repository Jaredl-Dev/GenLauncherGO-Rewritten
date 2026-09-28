using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenLauncherGO.Tests.Features.Launching;

public sealed class WindowsGameProcessLauncherTests
{
    private static readonly string _commandProcessorPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Fact]
    public async Task StartAsync_RunsTheExecutableWithItsArgumentsInItsDirectoryAsync()
    {
        using TestDirectory directory = new();
        string markerPath = directory.GetPath("working-directory.txt");

        IGameProcessLaunchOperation operation = await StartCommandProcessorAsync(
            $"/d /c cd >\"{markerPath}\"",
            TestContext.Current.CancellationToken);
        await operation.Completion.WaitAsync(TestTimeouts.Wait, TestContext.Current.CancellationToken);

        File.ReadAllText(markerPath).Trim().Should().BeEquivalentTo(Path.GetDirectoryName(_commandProcessorPath));
    }

    [Fact]
    public async Task Completion_WhenTheLaunchedProcessStartsAChild_WaitsForTheChildAsync()
    {
        long startedAt = Stopwatch.GetTimestamp();

        IGameProcessLaunchOperation operation = await StartCommandProcessorAsync(
            StartHandoffChild(pingCount: 3),
            TestContext.Current.CancellationToken);
        await operation.Completion.WaitAsync(TestTimeouts.Wait, TestContext.Current.CancellationToken);

        Stopwatch.GetElapsedTime(startedAt).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Completion_WhenTheStartTokenIsCanceledAfterStart_KeepsWaitingForTheGameAsync()
    {
        using CancellationTokenSource cancellation = new();
        IGameProcessLaunchOperation operation = await StartCommandProcessorAsync(
            StartHandoffChild(pingCount: 10),
            cancellation.Token);

        try
        {
            await cancellation.CancelAsync();

            Func<Task> completion = () => operation.Completion;
            await completion.Should().NotCompleteWithinAsync(TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            operation.ForceClose();
        }
    }

    [Fact]
    public async Task ForceClose_StopsTheProcessesTheLaunchStartedAsync()
    {
        IGameProcessLaunchOperation operation = await StartCommandProcessorAsync(
            StartHandoffChild(pingCount: 60),
            TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        operation.ForceClose();

        Func<Task> completion = () => operation.Completion.WaitAsync(
            TestTimeouts.Wait,
            TestContext.Current.CancellationToken);
        await completion.Should().NotThrowAsync();
    }

    /// <summary>
    ///     Makes cmd.exe start ping without waiting for it and exit, the way a game launcher hands off to the game. Ping
    ///     sends one echo per second, so the child outlives the launched process by about <paramref name="pingCount" />
    ///     seconds.
    /// </summary>
    private static string StartHandoffChild(int pingCount)
    {
        return $"/d /c start \"\" /b ping -n {pingCount} 127.0.0.1 >nul";
    }

    private static Task<IGameProcessLaunchOperation> StartCommandProcessorAsync(
        string arguments,
        CancellationToken cancellationToken)
    {
        WindowsGameProcessLauncher launcher = new(NullLogger<WindowsGameProcessLauncher>.Instance);
        return launcher.StartAsync(
            GameLaunchRequest.ForGameClient(_commandProcessorPath, arguments),
            cancellationToken);
    }
}
