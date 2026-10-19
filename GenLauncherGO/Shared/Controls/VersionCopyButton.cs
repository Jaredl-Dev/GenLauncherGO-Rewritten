using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace GenLauncherGO.Shared.Controls;

/// <summary>
///     Shows the launcher's version and copies it to the clipboard when clicked, for pasting into bug reports.
/// </summary>
/// <remarks>
///     The confirmation replaces the text in place for a moment rather than opening a dialog. When the clipboard is
///     held by another application and refuses the write, no confirmation appears, so a second click simply retries.
/// </remarks>
[PseudoClasses(CopiedPseudoClass)]
internal sealed class VersionCopyButton : Button
{
    /// <summary>
    ///     Identifies the <see cref="Text" /> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<VersionCopyButton, string?>(nameof(Text));

    /// <summary>
    ///     Identifies the <see cref="CopiedText" /> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> CopiedTextProperty =
        AvaloniaProperty.Register<VersionCopyButton, string?>(nameof(CopiedText));

    private const string CopiedPseudoClass = ":copied";

    private static readonly TimeSpan _confirmationDuration = TimeSpan.FromSeconds(1.6);

    private DispatcherTimer? _confirmationTimer;

    /// <summary>
    ///     Gets or sets the text that is shown and copied.
    /// </summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    ///     Gets or sets the confirmation shown briefly after a successful copy.
    /// </summary>
    public string? CopiedText
    {
        get => GetValue(CopiedTextProperty);
        set => SetValue(CopiedTextProperty, value);
    }

    /// <inheritdoc />
    protected override async void OnClick()
    {
        base.OnClick();

        string? text = Text;
        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (string.IsNullOrEmpty(text) || clipboard is null)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch (Exception exception) when (exception is ExternalException or TimeoutException)
        {
            return;
        }

        ShowConfirmation();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            // The text lives in the template, so screen readers would otherwise announce an unnamed button.
            AutomationProperties.SetName(this, Text);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _confirmationTimer?.Stop();
        PseudoClasses.Set(CopiedPseudoClass, false);
        base.OnDetachedFromVisualTree(e);
    }

    private void ShowConfirmation()
    {
        PseudoClasses.Set(CopiedPseudoClass, true);
        _confirmationTimer ??= CreateConfirmationTimer();
        _confirmationTimer.Stop();
        _confirmationTimer.Start();
    }

    private DispatcherTimer CreateConfirmationTimer()
    {
        DispatcherTimer timer = new() { Interval = _confirmationDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            PseudoClasses.Set(CopiedPseudoClass, false);
        };
        return timer;
    }
}
