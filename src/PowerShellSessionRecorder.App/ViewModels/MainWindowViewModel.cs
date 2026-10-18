using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.Services.Updates;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ApplicationSettings _settings;
    private readonly JsonSettingsStore _store;
    private readonly IThemeController? _themeController;
    private readonly IExternalLinkOpener? _links;
    private bool _suspendLanguage;
    private bool _suspendTheme;

    public MainWindowViewModel(
        LocalizationService localization,
        ApplicationSettings settings,
        JsonSettingsStore store,
        ISessionRecordingService? recordingService = null,
        IThemeController? themeController = null,
        IExternalLinkOpener? links = null,
        UpdateCheckCoordinator? updates = null)
    {
        _localization = localization;
        _settings = settings;
        _store = store;
        _themeController = themeController;
        _links = links;
        Updates = new UpdateStatusViewModel(
            localization,
            settings,
            store,
            updates ?? new UpdateCheckCoordinator(new UnavailableReleaseSource(), () => DateTimeOffset.UtcNow, ProductVersion.Current),
            links,
            SetStatus,
            new AvaloniaUiInvoker());
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

        Themes =
        [
            new ThemeOption(AppTheme.System, L.ThemeSystem, localization),
            new ThemeOption(AppTheme.Light, L.ThemeLight, localization),
            new ThemeOption(AppTheme.Dark, L.ThemeDark, localization)
        ];
        Session = new SessionSetupViewModel(localization, settings, store, RecentCustomerTags, SetStatus, recordingService, links);
        _suspendLanguage = true;
        SelectedLanguage = Languages.First(language => language.Code == localization.Language);
        _suspendLanguage = false;
        _suspendTheme = true;
        SelectedTheme = Themes.First(theme => theme.Theme == settings.Theme);
        _suspendTheme = false;
        _themeController?.Apply(settings.Theme);

        localization.LanguageChanged += (_, _) =>
        {
            foreach (var language in Languages)
                language.Refresh();
            foreach (var theme in Themes)
                theme.Refresh();
        };
        RecentCustomerTags.CollectionChanged += OnRecentTagsChanged;
    }

    public SessionSetupViewModel Session { get; }

    public UpdateStatusViewModel Updates { get; }

    public void BeginAutomaticUpdateCheck(CancellationToken cancellationToken)
        => Updates.BeginAutomaticCheck(cancellationToken);

    public ObservableCollection<string> RecentCustomerTags { get; }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public IReadOnlyList<ThemeOption> Themes { get; }

    public string ApplicationVersion => ProductVersion.Current;

    public string RepositoryUrl => RecordingPlatform.RepositoryUrl;

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
    private bool _isAboutPage;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

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

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_suspendTheme || value is null)
            return;

        if (_settings.Theme == value.Theme)
            return;

        _settings.Theme = value.Theme;
        _themeController?.Apply(value.Theme);
        if (!TrySave())
            return;

        StatusMessage = _localization.Get(L.StatusThemeChanged);
    }

    [RelayCommand]
    private void ShowSession() => SelectPage(session: true, history: false, settings: false, about: false);

    [RelayCommand]
    private void ShowHistory() => SelectPage(session: false, history: true, settings: false, about: false);

    [RelayCommand]
    private void ShowSettings() => SelectPage(session: false, history: false, settings: true, about: false);

    [RelayCommand]
    private void ShowAbout() => SelectPage(session: false, history: false, settings: false, about: true);

    [RelayCommand]
    private void OpenRepository()
    {
        if (_links?.TryOpen(RecordingPlatform.RepositoryUrl) != true)
            StatusMessage = _localization.Get(L.StatusLinkFailed);
    }

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

    private void SelectPage(bool session, bool history, bool settings, bool about)
    {
        IsSessionPage = session;
        IsHistoryPage = history;
        IsSettingsPage = settings;
        IsAboutPage = about;
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
