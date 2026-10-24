using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.Services.Updates;
using PowerShellSessionRecorder.Core;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.ViewModels;

/// <summary>
/// Read-only PowerShell version text. It does not download, install, or prompt.
/// </summary>
public sealed class PowerShellVersionStatusViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ApplicationSettings _settings;
    private readonly JsonSettingsStore _store;
    private readonly IInstalledPowerShellInspector _inspector;
    private readonly IPowerShellReleaseLookup _releases;
    private readonly IUiInvoker _ui;
    private readonly Func<DateTimeOffset> _utcNow;
    private int _started;
    private IReadOnlyList<PowerShellInstallInspection>? _installed;
    private string? _latestVersion;
    private bool _latestUnavailable;
    private string _installedText;
    private string _latestText;

    public PowerShellVersionStatusViewModel(
        LocalizationService localization,
        ApplicationSettings settings,
        JsonSettingsStore store,
        IInstalledPowerShellInspector inspector,
        IPowerShellReleaseLookup releases,
        IUiInvoker ui,
        Func<DateTimeOffset> utcNow)
    {
        _localization = localization;
        _settings = settings;
        _store = store;
        _inspector = inspector;
        _releases = releases;
        _ui = ui;
        _utcNow = utcNow;
        _installedText = localization.Get(L.PowerShellVersionsChecking);
        _latestText = localization.Get(L.PowerShellVersionsChecking);
        localization.LanguageChanged += (_, _) => RefreshLocalizedText();
    }

    public string InstalledText
    {
        get => _installedText;
        private set => SetProperty(ref _installedText, value);
    }

    public string LatestText
    {
        get => _latestText;
        private set => SetProperty(ref _latestText, value);
    }

    public void Begin(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        _ = RefreshAsync(cancellationToken);
    }

    public Task RefreshAsync(CancellationToken cancellationToken)
        => LoadAsync(cancellationToken);

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PowerShellInstallInspection> installed;
        try
        {
            installed = await Task.Run(_inspector.Inspect, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var latest = await ReadLatestAsync(cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
            return;

        await _ui.InvokeAsync(() =>
        {
            _installed = installed;
            _latestVersion = latest.Version;
            _latestUnavailable = latest.Unavailable;
            ApplyText();
        }).ConfigureAwait(false);
    }

    private async Task<(string? Version, bool Unavailable)> ReadLatestAsync(CancellationToken cancellationToken)
    {
        var cached = _settings.CachedPowerShell7StableVersion;
        if (!UpdateCheckSchedule.AutomaticCheckIsDue(_settings.LastPowerShellReleaseCheckUtc, _utcNow())
            && !string.IsNullOrWhiteSpace(cached))
            return (cached, false);

        GitHubFetchResult fetch;
        try
        {
            fetch = await _releases.GetLatestStableAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return (string.IsNullOrWhiteSpace(cached) ? null : cached, string.IsNullOrWhiteSpace(cached));
        }

        if (!PowerShellReleaseCatalog.TrySelectStable(fetch, out var version))
            return (string.IsNullOrWhiteSpace(cached) ? null : cached, string.IsNullOrWhiteSpace(cached));

        _settings.CachedPowerShell7StableVersion = version;
        _settings.LastPowerShellReleaseCheckUtc = _utcNow();
        try
        {
            _store.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return (version, false);
    }

    private void RefreshLocalizedText() => ApplyText();

    private void ApplyText()
    {
        if (_installed is null)
        {
            InstalledText = _localization.Get(L.PowerShellVersionsChecking);
            LatestText = _localization.Get(L.PowerShellVersionsChecking);
            return;
        }

        InstalledText = FormatInstalled(_installed);
        LatestText = _latestUnavailable || string.IsNullOrWhiteSpace(_latestVersion)
            ? _localization.Get(L.PowerShellLatestUnavailable)
            : _latestVersion;
    }

    private string FormatInstalled(IReadOnlyList<PowerShellInstallInspection> installed)
    {
        if (installed.Count == 0)
            return _localization.Get(L.PowerShellNotInstalled);

        var lines = new List<string>();
        foreach (var install in installed)
        {
            var family = install.Family == PowerShellVersion.WindowsPowerShell51
                ? _localization.Get(L.WindowsPowerShell51)
                : _localization.Get(L.PowerShell7);
            var version = string.IsNullOrWhiteSpace(install.ProductVersion)
                ? _localization.Get(L.PowerShellVersionUnreadable)
                : install.ProductVersion;
            var suffix = install.UsedForRecording
                ? " — " + _localization.Get(install.Family == PowerShellVersion.WindowsPowerShell51
                    ? L.PowerShell51RecordingUse
                    : L.PowerShell7RecordingUse)
                : "";
            lines.Add(family + " — " + version + suffix);
            lines.Add(string.IsNullOrWhiteSpace(install.ResolvedPath)
                || string.Equals(install.ResolvedPath, install.Path, StringComparison.Ordinal)
                ? install.Path
                : install.Path + " → " + install.ResolvedPath);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
