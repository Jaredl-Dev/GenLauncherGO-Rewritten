using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using GenLauncherGO.Features.Launching;
using GenLauncherGO.Features.Startup;

namespace GenLauncherGO.Tests.Testing;

/// <summary>
///     Runs a game-client launch whose game keeps running until the test lets it exit.
/// </summary>
internal sealed class ControllableGameProcessLaunch
{
    private readonly TaskCompletionSource<bool> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _running = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Task<bool> _launch = Task.FromResult(false);

    public ControllableGameProcessLaunch()
    {
        Operation = Substitute.For<IGameProcessLaunchOperation>();
        Operation.ExecutableName.Returns(LauncherFileSystemLayout.RetailGameExecutableFileName);
        Operation.Completion.Returns(_exit.Task);
        Launcher = Substitute.For<IGameProcessLauncher>();
        Launcher.StartAsync(Arg.Any<GameLaunchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Operation));
    }

    /// <summary>
    ///     Gets the running game, for asserting the commands the launcher sends to it.
    /// </summary>
    public IGameProcessLaunchOperation Operation { get; }

    /// <summary>
    ///     Gets the process launcher to give the launch coordinator under test.
    /// </summary>
    public IGameProcessLauncher Launcher { get; }

    /// <summary>
    ///     Starts the launch without waiting for it to finish.
    /// </summary>
    public void Start(LauncherLaunchCoordinator launchCoordinator, Window owner)
    {
        launchCoordinator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LauncherLaunchCoordinator.HasActiveProcess) &&
                launchCoordinator.HasActiveProcess)
            {
                _running.TrySetResult();
            }
        };
        _launch = TestLauncherLaunchCoordinator.LaunchGameClientAsync(launchCoordinator, owner);
    }

    /// <summary>
    ///     Waits until the launch coordinator reports the game as running.
    /// </summary>
    public Task WaitUntilRunningAsync()
    {
        return _running.Task.WaitAsync(TestTimeouts.Wait);
    }

    /// <summary>
    ///     Lets the game exit and waits for the launch workflow to finish. Calling it again has no further effect.
    /// </summary>
    public Task ExitAsync()
    {
        _exit.TrySetResult(true);
        return _launch.WaitAsync(TestTimeouts.Wait);
    }
}
