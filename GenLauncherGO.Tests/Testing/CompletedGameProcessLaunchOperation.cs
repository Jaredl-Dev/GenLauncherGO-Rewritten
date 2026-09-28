using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;

namespace GenLauncherGO.Tests.Testing;

internal sealed class CompletedGameProcessLaunchOperation : IGameProcessLaunchOperation
{
    public CompletedGameProcessLaunchOperation(bool succeeded, string executableName)
    {
        ExecutableName = executableName;
        Completion = Task.FromResult(succeeded);
    }

    public string ExecutableName { get; }

    public Task<bool> Completion { get; }

    public void ForceClose()
    {
    }
}
