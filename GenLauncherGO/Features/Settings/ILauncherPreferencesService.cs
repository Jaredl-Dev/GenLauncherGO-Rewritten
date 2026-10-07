using System;

namespace GenLauncherGO.Features.Settings;

/// <summary>
///     Provides the current launcher preferences and persists preference updates.
/// </summary>
internal interface ILauncherPreferencesService
{
    /// <summary>
    ///     Gets the current launcher preferences.
    /// </summary>
    LauncherPreferences Current { get; }

    /// <summary>
    ///     Occurs after launcher preferences have changed.
    /// </summary>
    event EventHandler<LauncherPreferences>? PreferencesChanged;

    /// <summary>
    ///     Persists the supplied launcher preferences and publishes the updated state.
    /// </summary>
    /// <exception cref="LauncherPreferencesPersistenceException">
    ///     Thrown when the requested preferences cannot be persisted. In that case,
    ///     <see cref="Current" /> and <see cref="PreferencesChanged" /> remain unchanged.
    /// </exception>
    void Update(LauncherPreferences preferences);

    /// <summary>
    ///     Saves the preferences that loading converted from an older format or reset, and does nothing otherwise.
    /// </summary>
    /// <remarks>
    ///     Loading never writes, so startup can confirm the launcher's location and folder access before the
    ///     preferences file is touched.
    /// </remarks>
    /// <exception cref="LauncherPreferencesPersistenceException">
    ///     Thrown when the loaded preferences cannot be persisted.
    /// </exception>
    void PersistLoadedPreferences();
}
