using System;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using GenLauncherGO.Shared.Localization;

namespace GenLauncherGO.Features.Startup;

/// <summary>
///     Presents one selectable game on the initial game-choice screen and in the main window's game switcher.
/// </summary>
/// <remarks>
///     Both screens show one of these per supported game, so the option markup exists once regardless of how many
///     games the launcher manages.
/// </remarks>
internal sealed class LauncherGameChoiceViewModel : ObservableObject
{
    private readonly string _addFolderToolTip;

    private readonly string _switchToolTip;

    public LauncherGameChoiceViewModel(
        SupportedGame game,
        ILauncherStringLocalizer stringLocalizer,
        bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(stringLocalizer);

        Game = game;
        DisplayName = stringLocalizer[PerGame.FullNameKey(game)];
        ShortName = stringLocalizer[PerGame.ShortNameKey(game)];
        SelectedText = stringLocalizer["Selected"];
        NotSelectedText = stringLocalizer["NotSelected"];
        _switchToolTip = string.Format(CultureInfo.CurrentCulture, stringLocalizer["SwitchToGame"], ShortName);
        _addFolderToolTip = string.Format(
            CultureInfo.CurrentCulture,
            stringLocalizer["AddGameFolderToolTip"],
            ShortName);
        IsSelected = isSelected;
        UpdateSwitchToolTip();
    }

    public SupportedGame Game { get; }

    public string DisplayName { get; }

    public string ShortName { get; }

    public string SelectedText { get; }

    public string NotSelectedText { get; }

    /// <remarks>
    ///     Resolved on first read so the options can be built and asserted on without an initialized Avalonia
    ///     application.
    /// </remarks>
    public Bitmap CoverImage => LauncherGameCovers.Get(Game);

    public bool IsSelected
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateSwitchToolTip();
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether the game has an installation folder the launcher can switch to.
    /// </summary>
    public bool IsAvailable
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateSwitchToolTip();
            }
        }
    } = true;

    /// <summary>
    ///     Gets what clicking this game in the main window's switcher does, or nothing for the managed game.
    /// </summary>
    public string? SwitchToolTip
    {
        get;
        private set => SetProperty(ref field, value);
    }

    private void UpdateSwitchToolTip()
    {
        SwitchToolTip = IsSelected ? null : IsAvailable ? _switchToolTip : _addFolderToolTip;
    }
}
