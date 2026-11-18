using System;
using CommunityToolkit.Mvvm.Input;
using GenLauncherGO.Shared.Dialogs;

namespace GenLauncherGO.Features.Mods;

internal sealed class InfoDialogViewModel
{
    public InfoDialogViewModel(
        LauncherInfoDialogRequest request,
        InfoDialogKind kind,
        string? continueText = null,
        string? cancelText = null,
        string? actionText = null,
        string? secondaryActionText = null,
        Action<string>? openLink = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        MainMessage = request.MainMessage;
        DetailMessage = request.DetailMessage;
        DetailFontSize = request.DetailFontSize ?? 15D;
        LinkText = request.LinkText;
        LinkUri = request.LinkUri;
        IsLinkVisible = LinkUri != null;
        if (IsLinkVisible)
        {
            ArgumentNullException.ThrowIfNull(openLink);
        }

        ContinueText = string.IsNullOrWhiteSpace(continueText) ? null : continueText;
        CancelText = string.IsNullOrWhiteSpace(cancelText) ? "Cancel" : cancelText;
        ActionText = string.IsNullOrWhiteSpace(actionText) ? null : actionText;
        SecondaryActionText = string.IsNullOrWhiteSpace(secondaryActionText) ? null : secondaryActionText;
        IsInfoAction = kind == InfoDialogKind.InfoAction;
        IsChoice = kind == InfoDialogKind.Choice;
        OkCommand = new RelayCommand(Accept);
        CancelCommand = new RelayCommand(Cancel);
        ContinueCommand = OkCommand;
        ActionCommand = new RelayCommand(CompleteAction);
        SecondaryActionCommand = new RelayCommand(CompleteSecondaryAction);
        OpenLinkCommand = new RelayCommand(() => openLink!(LinkUri!));
        CloseCommand = new RelayCommand(Close);

        IsWarningConfirmation = kind == InfoDialogKind.WarningConfirmation;
        IsOkVisible = kind is InfoDialogKind.Info or InfoDialogKind.Error;
        IsActionVisible = (IsInfoAction || IsChoice) && ActionText != null;
        IsSecondaryActionVisible = IsChoice && SecondaryActionText != null;
        IsContinueVisible = kind == InfoDialogKind.WarningConfirmation;
        IsCancelVisible = kind is InfoDialogKind.WarningConfirmation or InfoDialogKind.InfoAction or
            InfoDialogKind.Choice;
        IsInfoIconVisible = kind is InfoDialogKind.Info or InfoDialogKind.InfoAction or InfoDialogKind.Choice;
        IsWarningIconVisible = kind == InfoDialogKind.WarningConfirmation;
        IsErrorIconVisible = kind == InfoDialogKind.Error;
    }

    public string MainMessage { get; }

    public string DetailMessage { get; }

    public double DetailFontSize { get; }

    public string? ContinueText { get; }

    public string CancelText { get; }

    public string? ActionText { get; }

    public string? SecondaryActionText { get; }

    public string? LinkText { get; }

    public string? LinkUri { get; }

    public bool IsLinkVisible { get; }

    public bool IsOkVisible { get; }

    public bool IsContinueVisible { get; }

    public bool IsActionVisible { get; }

    public bool IsSecondaryActionVisible { get; }

    public bool IsCancelVisible { get; }

    public bool IsInfoIconVisible { get; }

    public bool IsWarningIconVisible { get; }

    public bool IsErrorIconVisible { get; }

    public IRelayCommand OkCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand ContinueCommand { get; }

    public IRelayCommand ActionCommand { get; }

    public IRelayCommand SecondaryActionCommand { get; }

    public IRelayCommand OpenLinkCommand { get; }

    public IRelayCommand CloseCommand { get; }

    /// <summary>
    ///     Gets the result requested by the dialog command.
    /// </summary>
    /// <remarks>
    ///     A choice reports its primary action as <see langword="true" />, its secondary action as
    ///     <see langword="false" />, and a cancel as <see langword="null" />, so dismissing it never picks an action.
    /// </remarks>
    public bool? DialogResult { get; private set; }

    private bool IsWarningConfirmation { get; }

    private bool IsInfoAction { get; }

    private bool IsChoice { get; }

    /// <summary>
    ///     Occurs when the view model requests that the owning dialog close.
    /// </summary>
    public event EventHandler? CloseRequested;

    private void Accept()
    {
        CompleteDialog(!IsInfoAction);
    }

    private void CompleteAction()
    {
        CompleteDialog(true);
    }

    private void CompleteSecondaryAction()
    {
        CompleteDialog(false);
    }

    private void Cancel()
    {
        CompleteDialog(IsChoice ? null : false);
    }

    private void CompleteDialog(bool? result)
    {
        DialogResult = result;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Close()
    {
        if (IsWarningConfirmation || IsInfoAction || IsChoice)
        {
            Cancel();
            return;
        }

        Accept();
    }
}
