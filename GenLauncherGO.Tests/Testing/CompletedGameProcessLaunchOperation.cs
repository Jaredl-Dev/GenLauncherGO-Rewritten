using System.Threading.Tasks;
using GenLauncherGO.Features.Launching;

namespace GenLauncherGO.Tests.Testing;

internal sealed class CompletedGameProcessLaunchOperation : IGameProcessLaunchOperation
{
    public CompletedGameProcessLaunchOperation(bool succeeded)
    {
        Completion = Task.FromResult(succeeded);
    }

    public Task<bool> Completion { get; }

    public void ForceClose()
    {
    }
}
