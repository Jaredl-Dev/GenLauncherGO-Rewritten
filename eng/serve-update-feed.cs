#:sdk Microsoft.NET.Sdk.Web
#:property RunWorkingDirectory=$(MSBuildStartupDirectory)

using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace GenLauncherGO.Build;

/// <summary>
///     Serves the locally packaged release feed for the portable update smoke test. It needs only the ASP.NET Core
///     runtime that every .NET SDK includes, so it runs wherever the repository builds.
/// </summary>
/// <remarks>
///     The address must match the temporary <c>SimpleWebSource</c> edit the smoke test describes. It listens on the
///     loopback address only, so the feed is not reachable from other machines. Like <c>package-release.cs</c>, it runs
///     from the repository root: the Web SDK would otherwise start it in this script's folder, so the
///     <c>RunWorkingDirectory</c> property above keeps the folder the command ran from. The SDK's build cache keeps
///     the folder of the first run, so always run it with <c>--no-cache</c>.
/// </remarks>
internal static class ServeUpdateFeed
{
    private const string FeedAddress = "http://127.0.0.1:8123";

    private static int Main()
    {
        string feedDirectory = Path.Combine(Environment.CurrentDirectory, "artifacts", "release");
        if (!Directory.Exists(feedDirectory))
        {
            Console.Error.WriteLine(
                $"No packaged feed at {feedDirectory}. Package a release and run this from the repository root.");
            return 1;
        }

        FileExtensionContentTypeProvider contentTypes = new();

        // Velopack packages are .nupkg files, which static file serving refuses until their type is known.
        contentTypes.Mappings[".nupkg"] = "application/octet-stream";

        using PhysicalFileProvider feedFiles = new(feedDirectory);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // The default hosting logs repeat the address and name the working directory instead of the feed, so this
        // script prints its own line and reports a failed start itself.
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = feedFiles,
            ContentTypeProvider = contentTypes
        });
        app.Lifetime.ApplicationStarted.Register(
            () => Console.WriteLine($"Serving {feedDirectory} at {FeedAddress}. Press Ctrl+C to stop."));

        try
        {
            app.Run(FeedAddress);
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Could not start the feed server: {exception.Message}");
            return 1;
        }

        return 0;
    }
}
