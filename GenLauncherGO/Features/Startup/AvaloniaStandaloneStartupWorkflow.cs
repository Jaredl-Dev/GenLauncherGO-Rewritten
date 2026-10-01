using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using GenLauncherGO.Features.Launcher;
using GenLauncherGO.Features.Settings;
using GenLauncherGO.Features.Startup.Views;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.Localization;
using GenLauncherGO.Shared.Themes;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Implements standalone startup setup, location blocking, and initial game selection with Avalonia windows.
/// </summary>
internal sealed class AvaloniaStandaloneStartupWorkflow : IStandaloneStartupWorkflow
{
    private readonly ILauncherFilePicker _filePicker;
    private readonly ILauncherHostEnvironmentService _hostEnvironmentService;
    private readonly IGameInstallationService _installationService;
    private readonly IStartupDialogService _startupDialogService;
    private readonly ILauncherStringLocalizer _stringLocalizer;

    public AvaloniaStandaloneStartupWorkflow(
        IGameInstallationService installationService,
        ILauncherHostEnvironmentService hostEnvironmentService,
        ILauncherFilePicker filePicker,
        ILauncherStringLocalizer stringLocalizer,
        IStartupDialogService startupDialogService)
    {
        _installationService = installationService ?? throw new ArgumentNullException(nameof(installationService));
        _hostEnvironmentService = hostEnvironmentService ??
                                  throw new ArgumentNullException(nameof(hostEnvironmentService));
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
        _startupDialogService = startupDialogService ?? throw new ArgumentNullException(nameof(startupDialogService));
    }

    /// <inheritdoc />
    public async Task<bool> ShowBlockingLauncherLocationAsync(LauncherStoragePaths storagePaths)
    {
        ArgumentNullException.ThrowIfNull(storagePaths);

        if (_installationService.FindContainingGame(storagePaths.ExecutableDirectory) is not { } containingGame)
        {
            return false;
        }

        // Shown before a game is active, so publish the containing game's palette for the blocking message.
        if (Application.Current is { } application)
        {
            LauncherThemeResourceApplier.Apply(
                application.Resources,
                LauncherThemePresets.Create(containingGame),
                false);
        }

        await _startupDialogService.ShowMessageAsync(
            _stringLocalizer["StandaloneLocationRequired"],
            _stringLocalizer["StandaloneLocationBlockingDescription"]);
        return true;
    }

    /// <inheritdoc />
    public async Task<StandaloneStartupResult> RunAsync(
        LauncherStoragePaths storagePaths,
        ILauncherPreferencesService preferencesService)
    {
        ArgumentNullException.ThrowIfNull(storagePaths);
        ArgumentNullException.ThrowIfNull(preferencesService);

        while (true)
        {
            if (!TryValidateCompleteConfiguration(
                    storagePaths,
                    preferencesService.Current.Installations,
                    out LauncherInstallations validatedInstallations))
            {
                if (!await ShowSetupAsync(storagePaths, preferencesService))
                {
                    return StandaloneStartupResult.Canceled;
                }

                continue;
            }

            SupportedGame? selectedGame = validatedInstallations.ResolvePreferredGame(
                preferencesService.Current.LastSelectedGame);
            if (selectedGame == null)
            {
                LauncherGameSelectionWindow selectionWindow = new(
                    new LauncherGameSelectionViewModel(preferencesService, _stringLocalizer),
                    _stringLocalizer,
                    _startupDialogService);
                await ShowWindowAsync(selectionWindow);
                if (!selectionWindow.Accepted)
                {
                    return StandaloneStartupResult.Canceled;
                }

                selectedGame = preferencesService.Current.LastSelectedGame;
            }

            if (selectedGame is not { } activeGame)
            {
                continue;
            }

            string? activePath = validatedInstallations.GetPath(activeGame);
            if (string.IsNullOrWhiteSpace(activePath))
            {
                continue;
            }

            if (preferencesService.Current.LastSelectedGame != activeGame)
            {
                LauncherPreferences updated = preferencesService.Current with { LastSelectedGame = activeGame };
                if (!LauncherPreferencesUpdate.TryApply(preferencesService, updated))
                {
                    var failure =
                        LauncherInfoDialogRequest.CreateSettingsSaveFailure(_stringLocalizer);
                    await _startupDialogService.ShowMessageAsync(failure.MainMessage, failure.DetailMessage);
                    return StandaloneStartupResult.Canceled;
                }
            }

            return StandaloneStartupResult.Ready(activeGame, activePath);
        }
    }

    private async Task<bool> ShowSetupAsync(
        LauncherStoragePaths storagePaths,
        ILauncherPreferencesService preferencesService)
    {
        var installations = new LauncherInstallationsViewModel(
            preferencesService.Current.Installations,
            storagePaths,
            _installationService,
            _hostEnvironmentService,
            _filePicker,
            _stringLocalizer);
        LauncherSetupWindow setupWindow = new(
            new LauncherSetupViewModel(preferencesService, installations),
            _stringLocalizer,
            _startupDialogService);
        await ShowWindowAsync(setupWindow);
        return setupWindow.Accepted;
    }

    private static Task ShowWindowAsync(Window window)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += WindowClosed;
        window.Show();
        return completion.Task;

        void WindowClosed(object? sender, EventArgs e)
        {
            window.Closed -= WindowClosed;
            completion.TrySetResult();
        }
    }

    private bool TryValidateCompleteConfiguration(
        LauncherStoragePaths storagePaths,
        LauncherInstallations installations,
        out LauncherInstallations validatedInstallations)
    {
        LauncherInstallationsValidationResult validation = _installationService.ValidateInstallations(
            installations,
            storagePaths.ExecutableDirectory);
        validatedInstallations = validation.CanonicalInstallations;
        return validation.IsValid;
    }
}
