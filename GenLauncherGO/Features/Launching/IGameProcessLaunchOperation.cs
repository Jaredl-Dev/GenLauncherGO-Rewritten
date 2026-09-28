using System.Threading.Tasks;

namespace GenLauncherGO.Features.Launching;

/// <summary>
///     Represents a launched game or tool and every process it starts, which can be observed and force closed.
/// </summary>
internal interface IGameProcessLaunchOperation
{
    /// <summary>
    ///     Gets the executable name of the launched process.
    /// </summary>
    string ExecutableName { get; }

    /// <summary>
    ///     Gets the task that completes when the launched process and every process it started have exited.
    /// </summary>
    Task<bool> Completion { get; }

    /// <summary>
    ///     Force closes the launched process and every process it started.
    /// </summary>
    void ForceClose();
}
