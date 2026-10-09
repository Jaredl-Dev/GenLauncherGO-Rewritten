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
    ///     The Win32 error <c>ERROR_ELEVATION_REQUIRED</c>, which Windows reports when an executable must run elevated,
    ///     for example because "Run this program as an administrator" is set on it.
    /// </summary>
    const int ErrorElevationRequired = 740;

    /// <summary>
    ///     Starts the requested game or tool process and returns an operation that tracks it and the processes it starts.
    /// </summary>
    /// <remarks>
    ///     <paramref name="cancellationToken" /> can only stop the start. Once the process is running, tracking continues
    ///     until every launched process exits, because deployment cleanup waits for it.
    /// </remarks>
    /// <exception cref="Win32Exception">
    ///     <see cref="Win32Exception.NativeErrorCode" /> is <see cref="ErrorElevationRequired" /> when Windows will only
    ///     start the executable as administrator and the launcher is not running as one.
    /// </exception>
    Task<IGameProcessLaunchOperation> StartAsync(
        GameLaunchRequest request,
        CancellationToken cancellationToken);
}
