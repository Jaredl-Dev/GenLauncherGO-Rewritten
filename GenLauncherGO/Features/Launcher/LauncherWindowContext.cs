using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace GenLauncherGO.Features.Launcher;

/// <summary>
///     Carries the main window's identity into the workflows that act on it.
/// </summary>
/// <remarks>
///     These three travel together through every window-owning workflow, so they are validated once here rather than
///     re-checked at each entry point. The window builds one of these and passes it per call: the workflow coordinator
///     is a singleton and must not hold window state of its own.
/// </remarks>
internal sealed class LauncherWindowContext(
    MainWindowViewModel viewModel,
    LauncherWindowListController content,
    Window owner)
{
    /// <summary>
    ///     Gets the bindable state for the window.
    /// </summary>
    public MainWindowViewModel ViewModel { get; } = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    /// <summary>
    ///     Gets the controller for the window's content lists and tabs.
    /// </summary>
    public LauncherWindowListController Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>
    ///     Gets the window that owns dialogs raised by a workflow.
    /// </summary>
    public Window Owner { get; } = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>
    ///     Applies a change to the active catalog, then rebuilds the lists from it.
    /// </summary>
    /// <param name="change">Changes the catalog and reports whether it did; the lists are rebuilt only then.</param>
    /// <returns>The result of <paramref name="change" />.</returns>
    /// <remarks>
    ///     Tile selection and scroll position are UI-owned until a persistence boundary, so both are captured before the
    ///     change: a game switch saves them for the outgoing game before the active runtime changes, and the rebuild
    ///     restores them. Main controls stay disabled meanwhile, so nothing acts on tiles that are about to be replaced.
    /// </remarks>
    public async Task<bool> ReloadListsAsync(Func<Task<bool>> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        ViewModel.SetMainControlsEnabled(false);
        try
        {
            Content.SaveModsListVerticalOffset();
            ViewModel.ApplySelectionToPersistenceModel();
            if (!await change())
            {
                return false;
            }

            ViewModel.ReloadForActiveGame();
            Content.Initialize();
            Content.RestoreModsListVerticalOffset();
            return true;
        }
        finally
        {
            ViewModel.SetMainControlsEnabled(true);
        }
    }
}
