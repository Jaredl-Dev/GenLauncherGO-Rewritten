using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GenLauncherGO.Shared.Themes;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Owns the Avalonia desktop lifetime and the launcher application host.
/// </summary>
internal sealed class LauncherAvaloniaApplication : Application
{
    private LauncherApplicationHost? _host;

    /// <summary>
    ///     Loads the Avalonia application resources and seeds the theme every control theme depends on.
    /// </summary>
    /// <remarks>
    ///     The control themes declared in App.axaml resolve launcher colours through <c>DynamicResource</c>, so those
    ///     keys have to exist at application scope before the first window is shown. Seeding here covers the startup
    ///     windows that run before a game is known; <see cref="LauncherRuntimeContext.Colors" /> replaces this with the
    ///     active game's palette as soon as one is resolved.
    /// </remarks>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LauncherThemeResourceApplier.Apply(Resources, LauncherThemePresets.Create(SupportedGame.ZeroHour));
    }

    /// <summary>
    ///     Starts the launcher after Avalonia has created its desktop lifetime.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _host = new LauncherApplicationHost();
            desktop.Exit += (_, _) =>
            {
                _host?.Shutdown();
                _host?.Dispose();
                _host = null;
            };

            PostStartup(() => _host.StartAsync(desktop), () => desktop.Shutdown());
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Queues launcher startup on the UI thread and shuts the application down when the launcher does not start.
    /// </summary>
    /// <remarks>
    ///     Startup begins synchronously inside the queued operation rather than in an async lambda, so the language it
    ///     applies outlives the operation; see <see cref="LauncherStartupCulture.Apply" />.
    /// </remarks>
    internal static void PostStartup(Func<Task<bool>> startAsync, Action shutdown)
    {
        Dispatcher.UIThread.Post(() => ShutdownUnlessStartedAsync(startAsync(), shutdown));
    }

    // Async void so a failed startup reaches the dispatcher's unhandled-exception boundary instead of becoming an
    // unobserved task.
    private static async void ShutdownUnlessStartedAsync(Task<bool> startup, Action shutdown)
    {
        if (!await startup)
        {
            shutdown();
        }
    }
}
