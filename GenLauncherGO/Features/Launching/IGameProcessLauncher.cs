using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace GenLauncherGO.Features.Launching;

/// <summary>
///     Launches supported game and tool processes for a prepared game directory.
/// </summary>
internal interface IGameProcessLauncher
{
    /// <summary>
    ///     Starts the requested game or tool process and returns an operation that tracks it and the processes it starts.
    /// </summary>
    /// <remarks>
    ///     <paramref name="cancellationToken" /> can only stop the start. Once the process is running, tracking continues
    ///     until every launched process exits, because deployment cleanup waits for it.
    /// </remarks>
    /// <exception cref="Win32Exception">
    ///     <see cref="Win32Exception.NativeErrorCode" /> is 740 (<c>ERROR_ELEVATION_REQUIRED</c>) when Windows will only
    ///     start the executable as administrator and the launcher is not running as one.
    /// </exception>
    Task<IGameProcessLaunchOperation> StartAsync(
        GameLaunchRequest request,
        CancellationToken cancellationToken);
}
