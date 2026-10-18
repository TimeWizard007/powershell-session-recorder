using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public partial class SessionSetupViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ApplicationSettings _settings;
    private readonly JsonSettingsStore _store;
    private readonly Action<string> _setStatus;
    private readonly string _previewUniqueId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private IFolderPicker? _folderPicker;
    private bool _suspendPersistence;

    public SessionSetupViewModel(
        LocalizationService localization,
        ApplicationSettings settings,
        JsonSettingsStore store,
        ObservableCollection<string> recentCustomerTags,
        Action<string> setStatus)
    {
        _localization = localization;
        _settings = settings;
        _store = store;
        _setStatus = setStatus;
        RecentCustomerTags = recentCustomerTags;
        // Generated property setters invoke change callbacks immediately. The choice list
        // must exist before hydration, because those callbacks refresh the preview.
        PowerShellChoices = CreateChoices();

        _suspendPersistence = true;
        LogDirectory = settings.LastLogDirectory;
        SelectedPowerShellVersion = settings.LastPowerShellVersion;
        IncludeDateAndTime = settings.IncludeDateAndTime;
        SaveSessionInHistory = settings.SaveSessionInHistory;
        SelectedPowerShellChoice = PowerShellChoices.FirstOrDefault(choice => choice.Version == SelectedPowerShellVersion)
            ?? PowerShellChoices[0];
        _suspendPersistence = false;

        localization.LanguageChanged += (_, _) => NotifyPresentation();
        recentCustomerTags.CollectionChanged += OnRecentTagsChanged;
    }

    public ObservableCollection<string> RecentCustomerTags { get; }

    public IReadOnlyList<PowerShellChoice> PowerShellChoices { get; }

    public bool ShowWindowsPowerShellNote => !OperatingSystem.IsWindows();

    public bool HasRecentTags => RecentCustomerTags.Count > 0;

    [ObservableProperty]
    private string _customerTag = "";

    [ObservableProperty]
    private string _sessionName = "";

    [ObservableProperty]
    private string _logDirectory = "";

    [ObservableProperty]
    private PowerShellVersion _selectedPowerShellVersion = PowerShellVersion.PowerShell7;

    [ObservableProperty]
    private PowerShellChoice? _selectedPowerShellChoice;

    [ObservableProperty]
    private bool _runAsAdministrator;

    [ObservableProperty]
    private bool _includeDateAndTime = true;

    [ObservableProperty]
    private bool _saveSessionInHistory = true;

    [ObservableProperty]
    private string? _selectedRecentTag;

    public bool HasCustomerTagError => CustomerTagError.Length > 0;

    public string CustomerTagError => ErrorText(
        SessionValidationError.CustomerTagRequired,
        L.ValidationCustomerTagRequired,
        SessionValidationError.CustomerTagNotFileSafe,
        L.ValidationCustomerTagNotFileSafe);

    public bool HasSessionNameError => SessionNameError.Length > 0;

    public string SessionNameError => ErrorText(
        SessionValidationError.SessionNameRequired,
        L.ValidationSessionNameRequired,
        SessionValidationError.SessionNameNotFileSafe,
        L.ValidationSessionNameNotFileSafe);

    public bool HasLogDirectoryWarning =>
        !string.IsNullOrWhiteSpace(LogDirectory) && !Directory.Exists(LogDirectory);

    public string LogDirectoryWarning =>
        HasLogDirectoryWarning ? _localization.Get(L.ValidationLogDirectoryMissing) : "";

    public string PreviewCustomerTag => DisplayOrNotSelected(CustomerTag);

    public string PreviewSessionName => DisplayOrNotSelected(SessionName);

    public string PreviewPowerShellVersion =>
        SelectedPowerShellChoice?.DisplayName ?? _localization.Get(L.PowerShell7);

    public string PreviewPrivilege => _localization.Get(
        RunAsAdministrator ? L.PrivilegeAdministrator : L.PrivilegeStandard);

    public string PreviewLogDirectory => DisplayOrNotSelected(LogDirectory);

    public string ProposedFileName
    {
        get
        {
            if (!CanBuildFileName())
                return _localization.Get(L.PreviewUnavailable);

            try
            {
                return TranscriptFilenameGenerator.BuildFileName(ToConfiguration(), DateTimeOffset.Now, _previewUniqueId);
            }
            catch (ArgumentException)
            {
                return _localization.Get(L.PreviewUnavailable);
            }
        }
    }

    public void AttachFolderPicker(IFolderPicker folderPicker) => _folderPicker = folderPicker;

    public void Flush()
    {
        RememberCustomerTag();
        Persist();
    }

    public void RememberCustomerTag()
    {
        if (!IsCustomerTagRememberable())
            return;

        var before = _settings.RecentCustomerTags.ToList();
        _settings.RememberCustomerTag(CustomerTag);
        if (before.SequenceEqual(_settings.RecentCustomerTags, StringComparer.Ordinal))
            return;

        ReplaceRecentTags();
        Persist();
    }

    [RelayCommand]
    private async Task BrowseLogDirectoryAsync()
    {
        if (_folderPicker is null)
        {
            _setStatus(_localization.Get(L.StatusLogFolderFailed));
            return;
        }

        try
        {
            var initial = Directory.Exists(LogDirectory) ? LogDirectory : null;
            var result = await _folderPicker.PickFolderAsync(_localization.Get(L.TooltipBrowse), initial);
            if (!result.Selected)
                return;

            if (result.IsNonLocal || string.IsNullOrWhiteSpace(result.LocalPath))
            {
                _setStatus(_localization.Get(L.StatusLogFolderNonLocal));
                return;
            }

            LogDirectory = result.LocalPath;
        }
        catch (Exception)
        {
            _setStatus(_localization.Get(L.StatusLogFolderFailed));
        }
    }

    partial void OnCustomerTagChanged(string value) => NotifyPresentation();

    partial void OnSessionNameChanged(string value) => NotifyPresentation();

    partial void OnLogDirectoryChanged(string value)
    {
        NotifyPresentation();
        Persist();
    }

    partial void OnSelectedPowerShellVersionChanged(PowerShellVersion value)
    {
        NotifyPresentation();
        Persist();
    }

    partial void OnSelectedPowerShellChoiceChanged(PowerShellChoice? value)
    {
        if (value is not null && value.Version != SelectedPowerShellVersion)
            SelectedPowerShellVersion = value.Version;
        else
            NotifyPresentation();
    }

    partial void OnRunAsAdministratorChanged(bool value) => NotifyPresentation();

    partial void OnIncludeDateAndTimeChanged(bool value)
    {
        NotifyPresentation();
        Persist();
    }

    partial void OnSaveSessionInHistoryChanged(bool value)
    {
        NotifyPresentation();
        Persist();
    }

    partial void OnSelectedRecentTagChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            CustomerTag = value;
    }

    private IReadOnlyList<PowerShellChoice> CreateChoices()
    {
        var choices = new List<PowerShellChoice>
        {
            new(PowerShellVersion.PowerShell7, _localization)
        };

        if (OperatingSystem.IsWindows())
            choices.Add(new PowerShellChoice(PowerShellVersion.WindowsPowerShell51, _localization));

        return choices;
    }

    private void NotifyPresentation()
    {
        foreach (var choice in PowerShellChoices)
            choice.Refresh();

        OnPropertyChanged(nameof(HasCustomerTagError));
        OnPropertyChanged(nameof(CustomerTagError));
        OnPropertyChanged(nameof(HasSessionNameError));
        OnPropertyChanged(nameof(SessionNameError));
        OnPropertyChanged(nameof(HasLogDirectoryWarning));
        OnPropertyChanged(nameof(LogDirectoryWarning));
        OnPropertyChanged(nameof(PreviewCustomerTag));
        OnPropertyChanged(nameof(PreviewSessionName));
        OnPropertyChanged(nameof(PreviewPowerShellVersion));
        OnPropertyChanged(nameof(PreviewPrivilege));
        OnPropertyChanged(nameof(PreviewLogDirectory));
        OnPropertyChanged(nameof(ProposedFileName));
    }

    private void Persist()
    {
        if (_suspendPersistence)
            return;

        _settings.LastLogDirectory = LogDirectory.Trim();
        _settings.LastPowerShellVersion = SelectedPowerShellVersion;
        _settings.IncludeDateAndTime = IncludeDateAndTime;
        _settings.SaveSessionInHistory = SaveSessionInHistory;
        _settings.Language = _localization.Language;

        try
        {
            _store.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _setStatus(_localization.Get(L.StatusSettingsSaveFailed));
        }
    }

    private void ReplaceRecentTags()
    {
        RecentCustomerTags.Clear();
        foreach (var tag in _settings.RecentCustomerTags)
            RecentCustomerTags.Add(tag);
    }

    private void OnRecentTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(HasRecentTags));

    private bool CanBuildFileName()
    {
        var validation = CurrentValidation();
        return !validation.HasError(SessionValidationError.CustomerTagRequired)
            && !validation.HasError(SessionValidationError.SessionNameRequired)
            && !validation.HasError(SessionValidationError.CustomerTagNotFileSafe)
            && !validation.HasError(SessionValidationError.SessionNameNotFileSafe);
    }

    private bool IsCustomerTagRememberable()
    {
        var validation = CurrentValidation();
        return !validation.HasError(SessionValidationError.CustomerTagRequired)
            && !validation.HasError(SessionValidationError.CustomerTagNotFileSafe);
    }

    private string ErrorText(string requiredCode, string requiredKey, string unsafeCode, string unsafeKey)
    {
        var validation = CurrentValidation();
        if (validation.HasError(requiredCode))
            return _localization.Get(requiredKey);
        if (validation.HasError(unsafeCode))
            return _localization.Get(unsafeKey);
        return "";
    }

    private string DisplayOrNotSelected(string value)
        => string.IsNullOrWhiteSpace(value) ? _localization.Get(L.PreviewNotSelected) : value.Trim();

    private SessionValidationResult CurrentValidation()
        => SessionConfigurationValidator.Validate(ToConfiguration());

    private SessionConfiguration ToConfiguration()
        => new()
        {
            CustomerTag = CustomerTag,
            SessionName = SessionName,
            LogDirectory = LogDirectory,
            PowerShellVersion = SelectedPowerShellVersion,
            RunAsAdministrator = RunAsAdministrator,
            IncludeDateAndTime = IncludeDateAndTime,
            SaveSessionInHistory = SaveSessionInHistory
        };
}
