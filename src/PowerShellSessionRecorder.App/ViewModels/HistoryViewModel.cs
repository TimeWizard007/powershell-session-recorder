using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ISessionHistoryStore _store;
    private readonly ITranscriptLocationOpener _opener;
    private readonly ITextClipboard _clipboard;
    private readonly ITranscriptStatusProbe _probe;
    private readonly Action<string> _setStatus;
    private readonly List<SessionHistoryEntry> _sessions = [];
    private readonly Dictionary<string, TranscriptObservation> _observations = new(StringComparer.OrdinalIgnoreCase);
    private bool _suspend;

    public HistoryViewModel(
        LocalizationService localization,
        ISessionHistoryStore store,
        ITranscriptLocationOpener opener,
        ITextClipboard clipboard,
        Action<string> setStatus,
        ITranscriptStatusProbe? probe = null)
    {
        _localization = localization;
        _store = store;
        _opener = opener;
        _clipboard = clipboard;
        _probe = probe ?? new TranscriptStatusProbe();
        _setStatus = setStatus;
        TagFilters = [];
        Entries = [];
        localization.LanguageChanged += (_, _) =>
        {
            RebuildTagFilters();
            ApplyFilter();
        };
        Refresh();
    }

    public ObservableCollection<TagFilterOption> TagFilters { get; }

    public ObservableCollection<HistoryEntryViewModel> Entries { get; }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private TagFilterOption? _selectedTag;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private string _storeWarning = "";

    [ObservableProperty]
    private string _actionMessage = "";

    public bool HasStoreWarning => !string.IsNullOrWhiteSpace(StoreWarning);

    public bool HasActionMessage => !string.IsNullOrWhiteSpace(ActionMessage);

    public bool ShowEmpty { get; private set; }

    public bool ShowNoMatches { get; private set; }

    public bool ShowList { get; private set; }

    public int IndexedSessionCount => _sessions.Count;

    partial void OnSearchTextChanged(string value)
    {
        if (!_suspend)
            ApplyFilter();
    }

    partial void OnSelectedTagChanged(TagFilterOption? value)
    {
        if (!_suspend)
            ApplyFilter();
    }

    partial void OnFromDateChanged(DateTime? value)
    {
        if (!_suspend)
            ApplyFilter();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        if (!_suspend)
            ApplyFilter();
    }

    partial void OnStoreWarningChanged(string value) => OnPropertyChanged(nameof(HasStoreWarning));

    partial void OnActionMessageChanged(string value) => OnPropertyChanged(nameof(HasActionMessage));

    [RelayCommand]
    public void Refresh()
    {
        HistoryCatalog catalog;
        try
        {
            catalog = _store.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            catalog = HistoryCatalog.Unreadable();
        }

        _sessions.Clear();
        _sessions.AddRange(catalog.Sessions);
        _observations.Clear();
        foreach (var entry in _sessions)
        {
            var observation = _probe.Observe(entry.TranscriptPath);
            _observations[entry.Id] = observation;
            if (observation.Status == SessionHistoryStatus.Finished
                && observation.EndedUtc is DateTimeOffset ended
                && (entry.RecordingStatus != SessionHistoryStatus.Finished || entry.EndedUtc != ended))
            {
                try
                {
                    _store.RecordCompletion(entry.Id, ended);
                    entry.RecordingStatus = SessionHistoryStatus.Finished;
                    entry.EndedUtc = ended;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                }
            }
        }

        StoreWarning = catalog.Kind switch
        {
            HistoryLoadKind.Unreadable => _localization.Get(L.HistoryStoreUnreadable),
            HistoryLoadKind.UnsupportedSchema => _localization.Get(L.HistoryStoreUnsupported),
            _ => ""
        };
        RebuildTagFilters();
        ApplyFilter();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _suspend = true;
        SearchText = "";
        FromDate = null;
        ToDate = null;
        SelectedTag = TagFilters.FirstOrDefault(option => option.Tag is null) ?? SelectedTag;
        _suspend = false;
        ApplyFilter();
    }

    private void RebuildTagFilters()
    {
        var previous = SelectedTag?.Tag;
        _suspend = true;
        TagFilters.Clear();
        TagFilters.Add(new TagFilterOption(null, _localization.Get(L.HistoryTagAll)));
        foreach (var tag in _sessions
                     .Select(entry => entry.CustomerTag)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            TagFilters.Add(new TagFilterOption(tag, tag));
        }

        SelectedTag = TagFilters.FirstOrDefault(option =>
                string.Equals(option.Tag, previous, StringComparison.OrdinalIgnoreCase))
            ?? TagFilters[0];
        _suspend = false;
    }

    private void ApplyFilter()
    {
        var matches = SessionHistoryQuery.Apply(
            _sessions,
            SearchText,
            SelectedTag?.Tag,
            FromDate,
            ToDate);

        Entries.Clear();
        foreach (var entry in matches)
            Entries.Add(CreateRow(entry));

        ShowEmpty = _sessions.Count == 0;
        ShowNoMatches = _sessions.Count > 0 && Entries.Count == 0;
        ShowList = Entries.Count > 0;
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(IndexedSessionCount));
    }

    private HistoryEntryViewModel CreateRow(SessionHistoryEntry entry)
    {
        _observations.TryGetValue(entry.Id, out var observation);
        var status = _observations.ContainsKey(entry.Id)
            ? observation.Status
            : SessionHistoryStatus.Unknown;

        var ended = status == SessionHistoryStatus.Finished
            ? observation.EndedUtc ?? entry.EndedUtc
            : null;

        return new HistoryEntryViewModel(
            entry,
            FormatLocal(entry.StartedUtc),
            ended is null ? "" : FormatLocal(ended.Value),
            StatusText(status),
            status == SessionHistoryStatus.TranscriptMissing,
            PowerShellText(entry.PowerShellVersion),
            () => OpenTranscript(entry.TranscriptPath),
            () => OpenFolder(entry.TranscriptPath),
            () => CopyPathAsync(entry.TranscriptPath));
    }

    private void OpenTranscript(string path) => Report(_opener.TryOpenFile(path), folder: false);

    private void OpenFolder(string path) => Report(_opener.TryOpenFolder(path), folder: true);

    private async Task CopyPathAsync(string path)
    {
        var copied = await _clipboard.TrySetTextAsync(path);
        var message = _localization.Get(copied ? L.StatusHistoryCopiedPath : L.StatusHistoryCopyFailed);
        ActionMessage = message;
        _setStatus(message);
    }

    private void Report(TranscriptOpenResult result, bool folder)
    {
        var message = Describe(result, folder);
        ActionMessage = message;
        _setStatus(message);
    }

    private string Describe(TranscriptOpenResult result, bool folder)
        => result switch
        {
            TranscriptOpenResult.Opened => _localization.Get(folder ? L.StatusHistoryOpenedFolder : L.StatusHistoryOpenedFile),
            TranscriptOpenResult.Missing => _localization.Get(L.StatusHistoryFileMissing),
            TranscriptOpenResult.AccessDenied => _localization.Get(L.StatusHistoryAccessDenied),
            TranscriptOpenResult.InvalidPath => _localization.Get(L.StatusHistoryInvalidPath),
            _ => _localization.Get(L.StatusHistoryOpenFailed)
        };

    private string StatusText(SessionHistoryStatus status)
        => _localization.Get(status switch
        {
            SessionHistoryStatus.Finished => L.HistoryStatusFinished,
            SessionHistoryStatus.Unknown => L.HistoryStatusUnknown,
            SessionHistoryStatus.TranscriptMissing => L.HistoryStatusMissing,
            _ => L.HistoryStatusRecordingConfirmed
        });

    private string PowerShellText(PowerShellVersion version)
        => version switch
        {
            PowerShellVersion.WindowsPowerShell51 => _localization.Get(L.WindowsPowerShell51),
            _ => _localization.Get(L.PowerShell7)
        };

    private static string FormatLocal(DateTimeOffset utc)
        => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

public sealed class TagFilterOption
{
    public TagFilterOption(string? tag, string display)
    {
        Tag = tag;
        Display = display;
    }

    public string? Tag { get; }

    public string Display { get; }

    public override string ToString() => Display;
}

public sealed class HistoryEntryViewModel : ObservableObject
{
    public HistoryEntryViewModel(
        SessionHistoryEntry entry,
        string startedLocalText,
        string endedLocalText,
        string statusText,
        bool statusIsMissing,
        string powerShellText,
        Action openTranscript,
        Action openFolder,
        Func<Task> copyPath)
    {
        CustomerTag = entry.CustomerTag;
        SessionName = entry.SessionName;
        TranscriptFileName = entry.TranscriptFileName;
        TranscriptPath = entry.TranscriptPath;
        StartedLocalText = startedLocalText;
        EndedLocalText = endedLocalText;
        HasEnded = endedLocalText.Length > 0;
        StatusText = statusText;
        StatusIsMissing = statusIsMissing;
        PowerShellText = powerShellText;
        OpenTranscriptCommand = new RelayCommand(openTranscript);
        OpenFolderCommand = new RelayCommand(openFolder);
        CopyPathCommand = new AsyncRelayCommand(copyPath);
    }

    public string CustomerTag { get; }

    public string SessionName { get; }

    public string TranscriptFileName { get; }

    public string TranscriptPath { get; }

    public string StartedLocalText { get; }

    public string EndedLocalText { get; }

    public bool HasEnded { get; }

    public string StatusText { get; }

    public bool StatusIsMissing { get; }

    public string PowerShellText { get; }

    public IRelayCommand OpenTranscriptCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand CopyPathCommand { get; }
}
