using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public partial class SessionSetupViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ApplicationSettings _settings;
    private readonly JsonSettingsStore _store;
    private readonly Action<string> _setStatus;
    private readonly ISessionRecordingService? _recordingService;
    private readonly IExternalLinkOpener? _links;
    private readonly ISessionHistoryStore? _history;
    private readonly Action? _historyChanged;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly string _previewUniqueId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private IFolderPicker? _folderPicker;
    private bool _suspendPersistence;

    public SessionSetupViewModel(
        LocalizationService localization,
        ApplicationSettings settings,
        JsonSettingsStore store,
        ObservableCollection<string> recentCustomerTags,
        Action<string> setStatus,
        ISessionRecordingService? recordingService = null,
        IExternalLinkOpener? links = null,
        ISessionHistoryStore? history = null,
        Action? historyChanged = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _localization = localization;
        _settings = settings;
        _store = store;
        _setStatus = setStatus;
        _recordingService = recordingService;
        _links = links;
        _history = history;
        _historyChanged = historyChanged;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
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
    private string _activityMessage = "";

    public bool HasActivityMessage => !string.IsNullOrWhiteSpace(ActivityMessage);

    partial void OnActivityMessageChanged(string value) => OnPropertyChanged(nameof(HasActivityMessage));

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

    public bool HasLogDirectoryError => IsLogDirectoryAFile();

    public string LogDirectoryError =>
        HasLogDirectoryError ? _localization.Get(L.ValidationLogDirectoryNotADirectory) : "";

    public bool HasLogDirectoryHint =>
        !string.IsNullOrWhiteSpace(LogDirectory) && !Directory.Exists(LogDirectory) && !HasLogDirectoryError;

    public string LogDirectoryHint =>
        HasLogDirectoryHint ? _localization.Get(L.ValidationLogDirectoryWillCreate) : "";

    public bool CanStartSession =>
        _recordingService is not null
        && !RunAsAdministrator
        && SelectedPowerShellVersion == PowerShellVersion.PowerShell7
        && !string.IsNullOrWhiteSpace(LogDirectory)
        && !HasLogDirectoryError
        && CurrentValidation().IsValid
        && _recordingService.Capabilities is
        {
            PlatformSupported: true,
            PowerShell7Available: true,
            TerminalAvailable: true
        };

    public bool ShowDependencyNotice =>
        ShowUnsupportedPlatform || ShowPowerShellMissing || ShowTerminalMissing;

    public bool ShowPowerShellMissing => _recordingService?.Capabilities.PowerShell7Available == false;

    public bool ShowTerminalMissing =>
        _recordingService?.Capabilities is { PlatformSupported: true, TerminalAvailable: false };

    public bool ShowUnsupportedPlatform =>
        _recordingService is not null && !_recordingService.Capabilities.PlatformSupported;

    public bool ShowDependencyRefresh => _recordingService is not null && ShowDependencyNotice;

    public string UbuntuTerminalInstallCommand => RecordingPlatform.UbuntuTerminalInstallCommand;

    public string StartSessionHint
    {
        get
        {
            if (_recordingService is null)
                return _localization.Get(L.ReasonRecordingRuntimeUnavailable);

            var capabilities = _recordingService.Capabilities;
            if (!capabilities.PlatformSupported)
                return _localization.Get(L.DependencyUnsupportedPlatform);
            if (!capabilities.PowerShell7Available && !capabilities.TerminalAvailable)
                return _localization.Get(L.ReasonPowerShellMissing) + " " + _localization.Get(L.ReasonTerminalMissing);
            if (!capabilities.PowerShell7Available)
                return _localization.Get(L.ReasonPowerShellMissing);
            if (!capabilities.TerminalAvailable)
                return _localization.Get(L.ReasonTerminalMissing);
            if (RunAsAdministrator)
                return _localization.Get(L.ReasonElevationUnavailable);
            if (SelectedPowerShellVersion != PowerShellVersion.PowerShell7)
                return _localization.Get(L.ReasonWindowsPowerShellUnsupported);
            if (string.IsNullOrWhiteSpace(LogDirectory))
                return _localization.Get(L.ValidationLogDirectoryRequired);
            if (HasLogDirectoryError)
                return _localization.Get(L.ValidationLogDirectoryNotADirectory);
            if (!CurrentValidation().IsValid)
                return _localization.Get(L.ReasonConfigurationInvalid);

            return _localization.Get(L.StartSessionReadyHint);
        }
    }

    public string StartSessionTooltip =>
        CanStartSession ? _localization.Get(L.TooltipStartSession) : StartSessionHint;

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

    [RelayCommand(CanExecute = nameof(CanStartSession), AllowConcurrentExecutions = true)]
    private async Task StartSessionAsync(CancellationToken cancellationToken)
    {
        if (_recordingService is null || !CanStartSession)
        {
            Report(StartSessionHint);
            return;
        }

        RememberCustomerTag();
        Report(_localization.Get(L.StatusRecordingInProgress));
        try
        {
            var configuration = ToConfiguration();
            var result = await _recordingService.StartAsync(configuration, _localization.Language, cancellationToken);
            Report(Describe(result) + RememberConfirmedSession(result, configuration));
        }
        catch (Exception)
        {
            Report(_localization.Get(L.StatusTerminalLaunchFailed));
        }
    }

    [RelayCommand]
    private void RefreshDependencies()
    {
        _recordingService?.RefreshDependencies();
        NotifyPresentation();
        Report(_localization.Get(L.StatusDependenciesRefreshed));
    }

    [RelayCommand]
    private void OpenPowerShellInstallDocs()
    {
        if (_links?.TryOpen(RecordingPlatform.PowerShellInstallDocumentation) != true)
            Report(_localization.Get(L.StatusLinkFailed));
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var result = LogFolderOpener.TryOpen(LogDirectory);
        Report(result switch
        {
            LogFolderOpenResult.Opened => _localization.Get(L.StatusLogFolderOpened),
            LogFolderOpenResult.EmptyPath => _localization.Get(L.StatusLogFolderEmpty),
            LogFolderOpenResult.Missing => _localization.Get(L.StatusLogFolderMissing),
            _ => _localization.Get(L.StatusLogFolderFailed)
        });
    }

    [RelayCommand]
    private async Task BrowseLogDirectoryAsync()
    {
        if (_folderPicker is null)
        {
            Report(_localization.Get(L.StatusLogFolderFailed));
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
                Report(_localization.Get(L.StatusLogFolderNonLocal));
                return;
            }

            LogDirectory = result.LocalPath;
        }
        catch (Exception)
        {
            Report(_localization.Get(L.StatusLogFolderFailed));
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
        OnPropertyChanged(nameof(HasLogDirectoryError));
        OnPropertyChanged(nameof(LogDirectoryError));
        OnPropertyChanged(nameof(HasLogDirectoryHint));
        OnPropertyChanged(nameof(LogDirectoryHint));
        OnPropertyChanged(nameof(CanStartSession));
        OnPropertyChanged(nameof(StartSessionHint));
        OnPropertyChanged(nameof(StartSessionTooltip));
        OnPropertyChanged(nameof(ShowDependencyNotice));
        OnPropertyChanged(nameof(ShowPowerShellMissing));
        OnPropertyChanged(nameof(ShowTerminalMissing));
        OnPropertyChanged(nameof(ShowUnsupportedPlatform));
        OnPropertyChanged(nameof(ShowDependencyRefresh));
        StartSessionCommand.NotifyCanExecuteChanged();
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
            Report(_localization.Get(L.StatusSettingsSaveFailed));
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

    private string RememberConfirmedSession(SessionRecordingResult result, SessionConfiguration configuration)
    {
        if (result.Status != SessionRecordingStatus.Started || string.IsNullOrWhiteSpace(result.TranscriptPath))
            return "";

        if (!configuration.SaveSessionInHistory || _history is null)
            return "";

        try
        {
            _history.Append(SessionHistoryEntry.CreateConfirmed(configuration, result.TranscriptPath, _utcNow()));
            _historyChanged?.Invoke();
            return "";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return " " + _localization.Get(L.StatusHistoryWriteFailed);
        }
    }

    private string Describe(SessionRecordingResult result)
        => result.Status switch
        {
            SessionRecordingStatus.Started => _localization.Get(L.StatusRecordingStarted)
                .Replace("{0}", result.TranscriptPath ?? "", StringComparison.Ordinal),
            SessionRecordingStatus.Unconfirmed => _localization.Get(L.StatusRecordingUnconfirmed),
            SessionRecordingStatus.DirectoryFailed => _localization.Get(L.StatusDirectoryCreateFailed),
            SessionRecordingStatus.AllocationFailed => _localization.Get(L.StatusTranscriptAllocationFailed),
            SessionRecordingStatus.LaunchFailed => _localization.Get(L.StatusTerminalLaunchFailed),
            _ => StartSessionHint
        };

    private bool DependenciesReady =>
        _recordingService?.Capabilities is
        {
            PlatformSupported: true,
            PowerShell7Available: true,
            TerminalAvailable: true
        };

    private void Report(string message)
    {
        ActivityMessage = message;
        _setStatus(message);
    }

    private bool IsLogDirectoryAFile()
        => !string.IsNullOrWhiteSpace(LogDirectory)
            && File.Exists(LogDirectory)
            && !Directory.Exists(LogDirectory);

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
