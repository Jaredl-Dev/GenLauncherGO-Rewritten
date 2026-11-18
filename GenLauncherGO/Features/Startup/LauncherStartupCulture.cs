using System.Globalization;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Applies the persisted launcher language before Avalonia views are composed.
/// </summary>
internal static class LauncherStartupCulture
{
    /// <summary>
    ///     Applies the persisted language preference to the current process.
    /// </summary>
    /// <remarks>
    ///     Call this synchronously from the dispatcher operation that starts the launcher, with no async method in
    ///     between. Avalonia's dispatcher keeps the UI-thread language an operation leaves behind and runs every later
    ///     operation under it. An async method discards a language change made before its first await when it
    ///     yields, so the dispatcher would keep the Windows language instead.
    /// </remarks>
    /// <param name="useEnglishLanguage">
    ///     <see langword="true" /> to force English; otherwise, to use the installed Windows UI culture.
    /// </param>
    public static void Apply(bool useEnglishLanguage)
    {
        CultureInfo culture = useEnglishLanguage
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.InstalledUICulture;

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
