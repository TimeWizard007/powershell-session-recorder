using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ApplicationSettings _settings;
    private readonly JsonSettingsStore _store;
    private bool _suspendLanguage;

    public MainWindowViewModel(
        LocalizationService localization,
        ApplicationSettings settings,
        JsonSettingsStore store)
    {
        _localization = localization;
        _settings = settings;
        _store = store;
        RecentCustomerTags = new ObservableCollection<string>(settings.RecentCustomerTags);
        Languages =
        [
            new LanguageOption("en-US", L.LanguageEnglish, localization),
            new LanguageOption("pl-PL", L.LanguagePolish, localization)
        ];

        if (!OperatingSystem.IsWindows()
            && settings.LastPowerShellVersion == PowerShellVersion.WindowsPowerShell51)
        {
            settings.LastPowerShellVersion = PowerShellVersion.PowerShell7;
            WindowsPowerShellWasAdjusted = true;
            TrySave();
        }

        Session = new SessionSetupViewModel(localization, settings, store, RecentCustomerTags, SetStatus);
        _suspendLanguage = true;
        SelectedLanguage = Languages.First(language => language.Code == localization.Language);
        _suspendLanguage = false;

        localization.LanguageChanged += (_, _) =>
        {
            foreach (var language in Languages)
                language.Refresh();
        };
        RecentCustomerTags.CollectionChanged += OnRecentTagsChanged;
    }

    public SessionSetupViewModel Session { get; }

    public ObservableCollection<string> RecentCustomerTags { get; }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public string SettingsFilePath => _store.FilePath;

    public bool WindowsPowerShellWasAdjusted { get; }

    public bool HasRecentTags => RecentCustomerTags.Count > 0;

    public bool ShowEmptyRecentTags => !HasRecentTags;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    [ObservableProperty]
    private bool _isSessionPage = true;

    [ObservableProperty]
    private bool _isHistoryPage;

    [ObservableProperty]
    private bool _isSettingsPage;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private string _statusMessage = "";

    public void Flush() => Session.Flush();

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_suspendLanguage || value is null)
            return;

        if (string.Equals(_localization.Language, value.Code, StringComparison.Ordinal))
            return;

        _localization.SetLanguage(value.Code);
        _settings.Language = _localization.Language;
        if (!TrySave())
            return;

        StatusMessage = _localization.Get(L.StatusLanguageChanged);
    }

    [RelayCommand]
    private void ShowSession() => SelectPage(session: true, history: false, settings: false);

    [RelayCommand]
    private void ShowHistory() => SelectPage(session: false, history: true, settings: false);

    [RelayCommand]
    private void ShowSettings() => SelectPage(session: false, history: false, settings: true);

    [RelayCommand]
    private void OpenLogFolder()
    {
        var result = LogFolderOpener.TryOpen(Session.LogDirectory);
        StatusMessage = result switch
        {
            LogFolderOpenResult.Opened => _localization.Get(L.StatusLogFolderOpened),
            LogFolderOpenResult.EmptyPath => _localization.Get(L.StatusLogFolderEmpty),
            LogFolderOpenResult.Missing => _localization.Get(L.StatusLogFolderMissing),
            _ => _localization.Get(L.StatusLogFolderFailed)
        };
    }

    private void SelectPage(bool session, bool history, bool settings)
    {
        IsSessionPage = session;
        IsHistoryPage = history;
        IsSettingsPage = settings;
    }

    private void SetStatus(string message) => StatusMessage = message;

    private bool TrySave()
    {
        try
        {
            _store.Save(_settings);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = _localization.Get(L.StatusSettingsSaveFailed);
            return false;
        }
    }

    private void OnRecentTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasRecentTags));
        OnPropertyChanged(nameof(ShowEmptyRecentTags));
    }
}
