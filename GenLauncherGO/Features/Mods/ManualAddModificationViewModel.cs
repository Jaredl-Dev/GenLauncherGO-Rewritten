using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenLauncherGO.Shared.Dialogs;
using GenLauncherGO.Shared.IO;
using GenLauncherGO.Shared.Localization;

namespace GenLauncherGO.Features.Mods;

/// <summary>
///     Provides bindable manual import fields, validation, and actions for launcher content.
/// </summary>
/// <remarks>
///     The name and version become folder names and the content's catalog identity, so they are trimmed and checked
///     against the same path rules the importer applies. Anything accepted here therefore installs under exactly the
///     identity the catalog later looks it up by. Which names other content already owns depends on the import's kind
///     and parent, so the import workflow supplies that check.
/// </remarks>
internal sealed class ManualAddModificationViewModel : ObservableObject
{
    private const string MissingModificationNameKey = "EnterModName";

    private const string MissingVersionKey = "EnterModVersion";

    private const string UnsupportedCharactersKey = "NameAndVersionValidSymbols";

    internal const string ReservedOrInvalidNameKey = "ReservedOrInvalidName";

    private const string ReservedOrInvalidVersionKey = "ReservedOrInvalidVersion";

    internal const string VersionAlreadyInstalledKey = "VersionAlreadyInstalled";

    private const string VersionMissingNumberKey = "VersionMustContainNumbers";

    private readonly ILauncherDialogService _dialogService;

    private readonly Func<string, string?> _getNameRefusal;

    private readonly Func<string, string, bool> _isVersionInstalled;

    private readonly ILauncherStringLocalizer _stringLocalizer;

    private string _modificationName;

    private string? _modificationNameValidationKey;

    private string? _versionValidationKey;

    public ManualAddModificationViewModel(
        IReadOnlyList<string> files,
        Func<string, string?> getNameRefusal,
        Func<string, string, bool> isVersionInstalled,
        ILauncherStringLocalizer stringLocalizer,
        ILauncherDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(files);

        _getNameRefusal = getNameRefusal ?? throw new ArgumentNullException(nameof(getNameRefusal));
        _isVersionInstalled = isVersionInstalled ?? throw new ArgumentNullException(nameof(isVersionInstalled));
        _stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _modificationName = InferModificationName(files);
        RefreshValidation();
        AcceptCommand = new AsyncRelayCommand(
            AcceptAsync,
            () => CanAccept,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCommand = new RelayCommand(Cancel);
    }

    public string ModificationName
    {
        get => _modificationName;
        set
        {
            string newValue = value ?? string.Empty;
            if (string.Equals(_modificationName, newValue, StringComparison.Ordinal))
            {
                return;
            }

            _modificationName = newValue;
            NotifyInputChanged(nameof(ModificationName));
        }
    }

    public string Version
    {
        get;
        set
        {
            string newValue = value ?? string.Empty;
            if (string.Equals(field, newValue, StringComparison.Ordinal))
            {
                return;
            }

            field = newValue;
            NotifyInputChanged(nameof(Version));
        }
    } = string.Empty;

    public string ModificationNameValidationMessage => GetLocalizedValidationMessage(_modificationNameValidationKey);

    public string VersionValidationMessage => GetLocalizedValidationMessage(_versionValidationKey);

    public bool CanAccept => GetFirstValidationKey() == null;

    public IAsyncRelayCommand AcceptCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public ManualModificationDialogResult? ImportResult { get; private set; }

    public bool? DialogResult { get; private set; }

    /// <summary>
    ///     Occurs when the view model requests that the owning dialog close.
    /// </summary>
    public event EventHandler? CloseRequested;

    private async Task AcceptAsync()
    {
        string? validationKey = GetFirstValidationKey();
        if (validationKey != null)
        {
            await ShowValidationErrorAsync(validationKey);
            return;
        }

        ImportResult = new ManualModificationDialogResult(
            ModificationName.Trim(),
            Version.Trim());
        CompleteDialog(true);
    }

    private void Cancel()
    {
        CompleteDialog(false);
    }

    private void CompleteDialog(bool accepted)
    {
        DialogResult = accepted;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string InferModificationName(IReadOnlyList<string> files)
    {
        string? firstFile = files.FirstOrDefault(file => !string.IsNullOrWhiteSpace(file));
        if (firstFile == null)
        {
            return string.Empty;
        }

        // A folder name has no extension, so a dot in it ("ShockWave 1.2") is part of the name.
        string fileName = Directory.Exists(firstFile)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(firstFile))
            : Path.GetFileNameWithoutExtension(firstFile);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return NormalizeInferredName(fileName);
    }

    private static string NormalizeInferredName(string fileName)
    {
        StringBuilder builder = new(fileName.Length);
        bool previousWasSpace = false;

        foreach (char character in fileName)
        {
            if (IsSupportedFieldCharacter(character))
            {
                builder.Append(character);
                previousWasSpace = char.IsWhiteSpace(character);
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private string? GetFirstValidationKey()
    {
        return _modificationNameValidationKey ?? _versionValidationKey;
    }

    /// <summary>
    ///     Validates both fields once per edit, so the messages, <see cref="CanAccept" />, and the command all read
    ///     the same result instead of each repeating the checks and the installed-version lookup.
    /// </summary>
    private void RefreshValidation()
    {
        _modificationNameValidationKey = GetModificationNameValidationKey();
        _versionValidationKey = GetVersionValidationKey(_modificationNameValidationKey == null);
    }

    private string? GetModificationNameValidationKey()
    {
        string name = ModificationName.Trim();
        if (name.Length == 0)
        {
            return MissingModificationNameKey;
        }

        if (!ContainsOnlySupportedFieldCharacters(name))
        {
            return UnsupportedCharactersKey;
        }

        return LexicalPath.IsValidPathSegment(name)
            ? _getNameRefusal(name)
            : ReservedOrInvalidNameKey;
    }

    private string? GetVersionValidationKey(bool isModificationNameValid)
    {
        string version = Version.Trim();
        if (version.Length == 0)
        {
            return MissingVersionKey;
        }

        if (!version.Any(character => character is >= '0' and <= '9'))
        {
            return VersionMissingNumberKey;
        }

        if (!ContainsOnlySupportedFieldCharacters(version))
        {
            return UnsupportedCharactersKey;
        }

        if (!LexicalPath.IsValidPathSegment(version))
        {
            return ReservedOrInvalidVersionKey;
        }

        // Refusing a listed version here spares the user the import's copying. The importer refuses again before
        // installing, for files another program put in the version's folder before the launcher listed them.
        return isModificationNameValid && _isVersionInstalled(ModificationName.Trim(), version)
            ? VersionAlreadyInstalledKey
            : null;
    }

    private string GetLocalizedValidationMessage(string? validationKey)
    {
        return validationKey == null ? string.Empty : _stringLocalizer[validationKey];
    }

    private static bool ContainsOnlySupportedFieldCharacters(string value)
    {
        return value.All(IsSupportedFieldCharacter);
    }

    /// <remarks>
    ///     Allows <c>&amp;</c> because several repository mods are named "C&amp;C …", and an import only matches its
    ///     repository mod when the user can enter that exact name.
    /// </remarks>
    private static bool IsSupportedFieldCharacter(char character)
    {
        return char.IsLetterOrDigit(character) ||
               character == '_' ||
               character == '.' ||
               character == '@' ||
               character == '&' ||
               character == '-' ||
               character == ' ';
    }

    private Task ShowValidationErrorAsync(string detailKey)
    {
        return _dialogService.ShowErrorAsync(new LauncherInfoDialogRequest(
            _stringLocalizer["OperationAborted"],
            _stringLocalizer[detailKey]));
    }

    /// <summary>
    ///     Refreshes both fields' messages after either changes, because whether a version is already installed
    ///     depends on the name as well.
    /// </summary>
    private void NotifyInputChanged(string fieldPropertyName)
    {
        RefreshValidation();
        OnPropertyChanged(fieldPropertyName);
        OnPropertyChanged(nameof(ModificationNameValidationMessage));
        OnPropertyChanged(nameof(VersionValidationMessage));
        OnPropertyChanged(nameof(CanAccept));
        AcceptCommand.NotifyCanExecuteChanged();
    }
}
