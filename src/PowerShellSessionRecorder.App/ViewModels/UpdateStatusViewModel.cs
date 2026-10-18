using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.Services.Updates;
using PowerShellSessionRecorder.Core;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.ViewModels;

public partial class UpdateStatusViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ApplicationSettings _settings;
    private readonly JsonSettingsStore _store;
    private readonly UpdateCheckCoordinator _coordinator;
    private readonly IExternalLinkOpener? _links;
    private readonly IUiInvoker _ui;
    private readonly Action<string> _setStatus;
    private CancellationToken _shutdown;
    private UpdateCheckOutcome? _lastOutcome;
    private string? _dismissedVersion;
    private bool _busy;

    public UpdateStatusViewModel(
        LocalizationService localization,
        ApplicationSettings settings,
        JsonSettingsStore store,
        UpdateCheckCoordinator coordinator,
        IExternalLinkOpener? links,
        Action<string> setStatus,
        IUiInvoker? ui = null)
    {
        _localization = localization;
        _settings = settings;
        _store = store;
        _coordinator = coordinator;
        _links = links;
        _setStatus = setStatus;
        _ui = ui ?? new InlineUiInvoker();
        RefreshPresentation();
        localization.LanguageChanged += (_, _) => RefreshPresentation();
    }

    public string CurrentVersion => _coordinator.CurrentVersion;

    public bool CheckForUpdatesAutomatically
    {
        get => _settings.CheckForUpdatesAutomatically;
        set
        {
            if (_settings.CheckForUpdatesAutomatically == value)
                return;

            _settings.CheckForUpdatesAutomatically = value;
            OnPropertyChanged();
            if (!TrySave())
                OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _showNotice;

    [ObservableProperty]
    private bool _hasReleasePage;

    [ObservableProperty]
    private string _updateNoticeBody = "";

    [ObservableProperty]
    private string _updateCheckMessage = "";

    [ObservableProperty]
    private string _lastCheckText = "";

    [ObservableProperty]
    private string _latestVersionText = "";

    public bool HasUpdateCheckMessage => !string.IsNullOrWhiteSpace(UpdateCheckMessage);

    private string? NoticePageUrl { get; set; }

    private string? NoticeAvailableVersion { get; set; }

    partial void OnUpdateCheckMessageChanged(string value) => OnPropertyChanged(nameof(HasUpdateCheckMessage));

    public void BeginAutomaticCheck(CancellationToken cancellationToken)
    {
        _shutdown = cancellationToken;
        if (!_settings.CheckForUpdatesAutomatically)
            return;

        if (!UpdateCheckSchedule.AutomaticCheckIsDue(_settings.LastUpdateCheckUtc, DateTimeOffset.UtcNow))
        {
            ApplyCachedNotice();
            return;
        }

        _ = RunAsync(manual: false, cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private Task CheckForUpdatesNowAsync(CancellationToken cancellationToken)
        => RunAsync(manual: true, cancellationToken);

    private bool CanCheckNow() => !_busy;

    private async Task RunAsync(bool manual, CancellationToken cancellationToken)
    {
        if (_busy)
            return;

        _busy = true;
        IsChecking = true;
        UpdateCheckMessage = _localization.Get(L.UpdatesChecking);
        CheckForUpdatesNowCommand.NotifyCanExecuteChanged();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_shutdown, cancellationToken);
            var outcome = await _coordinator.CheckAsync(_settings, manual, linked.Token).ConfigureAwait(false);
            await _ui.InvokeAsync(() =>
            {
                IsChecking = false;
                Apply(outcome);
            }).ConfigureAwait(false);
        }
        finally
        {
            await _ui.InvokeAsync(() =>
            {
                _busy = false;
                IsChecking = false;
                CheckForUpdatesNowCommand.NotifyCanExecuteChanged();
            }).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        _dismissedVersion = NoticeAvailableVersion;
        ShowNotice = false;
    }

    [RelayCommand]
    private void ViewRelease()
    {
        if (!ReleasePageLink.IsTrusted(NoticePageUrl) || _links?.TryOpen(NoticePageUrl!) != true)
            _setStatus(_localization.Get(L.StatusLinkFailed));
    }

    private void Apply(UpdateCheckOutcome outcome)
    {
        if (outcome.Kind != UpdateCheckKind.NotDue)
            _lastOutcome = outcome;

        if (outcome.CountsAsSuccessfulCheck)
            TrySave();

        UpdateCheckMessage = Describe(outcome);
        RefreshPresentation();

        if (outcome.Kind is UpdateCheckKind.UpToDate or UpdateCheckKind.NoPublishedRelease or UpdateCheckKind.IgnoredPrerelease)
        {
            ShowNotice = false;
            return;
        }

        if (outcome.Kind is not (UpdateCheckKind.UpdateAvailable or UpdateCheckKind.NotDue))
            return;

        ShowNoticeFor(outcome.LatestStableVersion, outcome.ReleasePageUrl);
    }

    private void ApplyCachedNotice()
    {
        ShowNoticeFor(_settings.LastKnownStableVersion, _settings.LastKnownReleaseUrl);
        RefreshPresentation();
    }

    private void ShowNoticeFor(string? version, string? pageUrl)
    {
        if (string.IsNullOrWhiteSpace(version)
            || ReleaseVersionComparison.Relate(CurrentVersion, version) != ReleaseRelation.NewerStable
            || string.Equals(version, _dismissedVersion, StringComparison.Ordinal))
            return;

        NoticeAvailableVersion = version;
        NoticePageUrl = ReleasePageLink.IsTrusted(pageUrl) ? pageUrl : ReleasePageLink.SelectTrustedPage(null, version);
        HasReleasePage = NoticePageUrl is not null;
        UpdateNoticeBody = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization.Get(L.UpdateNoticeBody),
            CurrentVersion,
            version);
        ShowNotice = true;
    }

    private void RefreshPresentation()
    {
        LastCheckText = _settings.LastUpdateCheckUtc is { } checkedAt
            ? checkedAt.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture)
            : _localization.Get(L.UpdatesLastCheckNever);

        LatestVersionText = !string.IsNullOrWhiteSpace(_settings.LastKnownStableVersion)
            ? _settings.LastKnownStableVersion
            : _settings.LastUpdateCheckUtc is not null
                ? _localization.Get(L.UpdatesNoPublishedReleases)
                : _localization.Get(L.UpdatesLatestUnknown);

        if (ShowNotice && NoticeAvailableVersion is not null)
        {
            UpdateNoticeBody = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization.Get(L.UpdateNoticeBody),
                CurrentVersion,
                NoticeAvailableVersion);
        }

        if (IsChecking)
            UpdateCheckMessage = _localization.Get(L.UpdatesChecking);
        else if (_lastOutcome is { } outcome)
            UpdateCheckMessage = Describe(outcome);
    }

    private string Describe(UpdateCheckOutcome outcome) => outcome.Kind switch
    {
        UpdateCheckKind.UpdateAvailable => string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization.Get(L.UpdatesAvailableStatus),
            outcome.LatestStableVersion,
            outcome.CurrentVersion),
        UpdateCheckKind.UpToDate or UpdateCheckKind.IgnoredPrerelease => _localization.Get(L.UpdatesUpToDate),
        UpdateCheckKind.NoPublishedRelease => _localization.Get(L.UpdatesNoPublishedReleases),
        UpdateCheckKind.InvalidRelease => _localization.Get(L.UpdatesInvalidRelease),
        UpdateCheckKind.Offline => _localization.Get(L.UpdatesOffline),
        UpdateCheckKind.TimedOut => _localization.Get(L.UpdatesTimedOut),
        UpdateCheckKind.RateLimited => _localization.Get(L.UpdatesRateLimited),
        UpdateCheckKind.MalformedResponse => _localization.Get(L.UpdatesInvalidResponse),
        UpdateCheckKind.Canceled => _localization.Get(L.UpdatesCanceled),
        _ => ""
    };

    private bool TrySave()
    {
        try
        {
            _store.Save(_settings);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _setStatus(_localization.Get(L.StatusSettingsSaveFailed));
            return false;
        }
    }
}
