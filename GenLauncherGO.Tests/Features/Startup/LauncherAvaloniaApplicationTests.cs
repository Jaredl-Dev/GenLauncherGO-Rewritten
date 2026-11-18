using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using GenLauncherGO.Features.Startup;

namespace GenLauncherGO.Tests.Features.Startup;

[Collection("Avalonia")]
public sealed class LauncherAvaloniaApplicationTests
{
    /// <summary>
    ///     Avalonia's dispatcher runs every later operation under the UI thread's language as it stood when the
    ///     previous operation ended, so the language startup applies has to survive the startup operation itself.
    /// </summary>
    [Fact]
    public void PostStartup_KeepsTheLanguageStartupAppliesForLaterDispatcherWork()
    {
        StaTestRunner.Run(() =>
        {
            CultureInfo originalCulture = CultureInfo.CurrentUICulture;
            TaskCompletionSource<bool> startup = new();
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

                LauncherAvaloniaApplication.PostStartup(
                    () =>
                    {
                        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
                        return startup.Task;
                    },
                    () => { });
                Dispatcher.UIThread.RunJobs();

                string? laterCulture = null;
                Dispatcher.UIThread.Post(() => laterCulture = CultureInfo.CurrentUICulture.Name);
                Dispatcher.UIThread.RunJobs();

                laterCulture.Should().Be("fr-FR");
            }
            finally
            {
                startup.TrySetResult(true);
                Dispatcher.UIThread.RunJobs();
                CultureInfo.CurrentUICulture = originalCulture;
            }
        });
    }
}
