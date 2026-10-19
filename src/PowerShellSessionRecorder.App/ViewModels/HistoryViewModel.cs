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
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _pendingIds = [];
    private bool _pendingDeletesFiles;
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

    [ObservableProperty]
    private string _pendingCleanupSummary = "";

    [ObservableProperty]
    private bool _hasPendingCleanup;

    public bool HasStoreWarning => !string.IsNullOrWhiteSpace(StoreWarning);

    public bool HasActionMessage => !string.IsNullOrWhiteSpace(ActionMessage);

    public bool ShowEmpty { get; private set; }

    public bool ShowNoMatches { get; private set; }

    public bool ShowList { get; private set; }

    public int IndexedSessionCount => _sessions.Count;

    public int SelectedCount => _selected.Count;

    public bool HasSelection => _selected.Count > 0;

    public string SelectionCountText => string.Format(_localization.Get(L.HistorySelectionCount), _selected.Count);

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

        var visible = new HashSet<string>(matches.Select(entry => entry.Id), StringComparer.OrdinalIgnoreCase);
        _selected.RemoveWhere(id => !visible.Contains(id));
        CancelCleanup();

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
        NotifySelection();
    }

    [RelayCommand]
    private void SelectAllShown()
    {
        foreach (var entry in Entries)
            entry.IsSelected = true;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ClearSelection()
    {
        foreach (var entry in Entries.ToList())
            entry.IsSelected = false;
        _selected.Clear();
        NotifySelection();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AskRemoveSelectedHistory() => AskCleanup(_selected.ToList(), deleteFiles: false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AskRemoveSelectedFiles() => AskCleanup(_selected.ToList(), deleteFiles: true);

    [RelayCommand]
    private void ConfirmCleanup()
    {
        if (!HasPendingCleanup)
            return;

        var visible = Entries.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = _pendingIds.Where(visible.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = _sessions.Where(entry => ids.Contains(entry.Id)).ToList();
        var deleteFiles = _pendingDeletesFiles;
        CancelCleanup();
        var report = HistoryCleanup.Remove(_store, entries, deleteFiles);
        Refresh();
        var message = DescribeCleanup(report);
        ActionMessage = message;
        _setStatus(message);
    }

    [RelayCommand]
    private void CancelCleanup()
    {
        _pendingIds = [];
        _pendingDeletesFiles = false;
        HasPendingCleanup = false;
        PendingCleanupSummary = "";
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

        var canDelete = status is SessionHistoryStatus.Finished or SessionHistoryStatus.TranscriptMissing;
        return new HistoryEntryViewModel(
            entry,
            FormatLocal(entry.StartedUtc),
            ended is null ? "" : FormatLocal(ended.Value),
            StatusText(status),
            StatusToolTip(status),
            status == SessionHistoryStatus.TranscriptMissing,
            canDelete,
            canDelete
                ? _localization.Get(L.HistoryCleanupFooterNote)
                : _localization.Get(L.HistoryDeleteFileUnavailable),
            PowerShellText(entry.PowerShellVersion),
            _selected.Contains(entry.Id),
            OnRowSelection,
            () => OpenTranscript(entry.TranscriptPath),
            () => OpenFolder(entry.TranscriptPath),
            () => CopyPathAsync(entry.TranscriptPath),
            () => AskCleanup([entry.Id], deleteFiles: false),
            () => AskCleanup([entry.Id], deleteFiles: true));
    }

    private void OnRowSelection(string id, bool selected)
    {
        if (selected)
            _selected.Add(id);
        else
            _selected.Remove(id);
        NotifySelection();
    }

    private void AskCleanup(IReadOnlyList<string> ids, bool deleteFiles)
    {
        var visible = Entries.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var chosen = _sessions.Where(entry => visible.Contains(entry.Id) && ids.Contains(entry.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        if (chosen.Count == 0)
            return;

        _pendingIds = chosen.Select(entry => entry.Id).ToList();
        _pendingDeletesFiles = deleteFiles;
        var fileTargets = deleteFiles ? chosen.Count(IsFileDeletionTarget) : 0;
        var unfinished = deleteFiles ? chosen.Count(entry => !IsFileDeletionTarget(entry) && !IsMissing(entry)) : 0;
        var missing = deleteFiles ? chosen.Count(IsMissing) : 0;
        var summary = deleteFiles
            ? string.Format(_localization.Get(L.HistoryCleanupWithFiles), chosen.Count, fileTargets)
            : string.Format(_localization.Get(L.HistoryCleanupHistoryOnly), chosen.Count);
        if (unfinished > 0)
            summary += " " + string.Format(_localization.Get(L.HistoryCleanupKeptUnfinished), unfinished);
        if (missing > 0)
            summary += " " + string.Format(_localization.Get(L.HistoryCleanupMissingFiles), missing);
        summary += " " + _localization.Get(L.HistoryCleanupFooterNote);
        PendingCleanupSummary = summary;
        HasPendingCleanup = true;
    }

    private bool IsFileDeletionTarget(SessionHistoryEntry entry)
    {
        if (!_observations.TryGetValue(entry.Id, out var observation)
            || observation.Status != SessionHistoryStatus.Finished)
            return false;

        if (UnixPrivateMetadata.IsSymbolicLink(entry.TranscriptPath) || Directory.Exists(entry.TranscriptPath))
            return false;

        return File.Exists(entry.TranscriptPath);
    }

    private bool IsMissing(SessionHistoryEntry entry)
        => _observations.TryGetValue(entry.Id, out var observation)
            && observation.Status == SessionHistoryStatus.TranscriptMissing;

    private string DescribeCleanup(HistoryCleanupReport report)
    {
        if (report.HistoryWriteFailed && report.FilesDeleted > 0)
            return _localization.Get(L.StatusHistoryWriteFailedAfterFiles);
        if (report.HistoryWriteFailed)
            return _localization.Get(L.StatusHistoryCleanupWriteFailed);

        var parts = new List<string>();
        if (!report.DeletesFiles)
            parts.Add(string.Format(_localization.Get(L.StatusHistoryRemovedOnly), report.HistoryRemoved));
        else
        {
            parts.Add(string.Format(_localization.Get(L.StatusHistoryRemovedWithFiles), report.HistoryRemoved, report.FilesDeleted));
            if (report.FilesAlreadyMissing > 0)
                parts.Add(string.Format(_localization.Get(L.StatusHistoryFilesMissing), report.FilesAlreadyMissing));
        }

        if (report.FilesPreserved > 0)
            parts.Add(string.Format(_localization.Get(L.StatusHistoryPreserved), report.FilesPreserved));
        if (report.FilesRefused > 0)
            parts.Add(string.Format(_localization.Get(L.StatusHistoryDeleteRefused), report.FilesRefused));
        if (parts.Count == 0)
            return _localization.Get(L.StatusHistoryNothingRemoved);

        return string.Join(" ", parts);
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCountText));
        ClearSelectionCommand.NotifyCanExecuteChanged();
        AskRemoveSelectedHistoryCommand.NotifyCanExecuteChanged();
        AskRemoveSelectedFilesCommand.NotifyCanExecuteChanged();
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

    private string? StatusToolTip(SessionHistoryStatus status)
        => status == SessionHistoryStatus.RecordingConfirmed
            ? _localization.Get(L.HistoryStatusRecordingConfirmedHint)
            : null;

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
    private readonly Action<string, bool> _selectionChanged;
    private bool _isSelected;

    public HistoryEntryViewModel(
        SessionHistoryEntry entry,
        string startedLocalText,
        string endedLocalText,
        string statusText,
        string? statusToolTip,
        bool statusIsMissing,
        bool canDeleteTranscript,
        string deleteFileToolTip,
        string powerShellText,
        bool isSelected,
        Action<string, bool> selectionChanged,
        Action openTranscript,
        Action openFolder,
        Func<Task> copyPath,
        Action removeFromHistory,
        Action removeAndDeleteFile)
    {
        Id = entry.Id;
        CustomerTag = entry.CustomerTag;
        SessionName = entry.SessionName;
        TranscriptFileName = entry.TranscriptFileName;
        TranscriptPath = entry.TranscriptPath;
        StartedLocalText = startedLocalText;
        EndedLocalText = endedLocalText;
        HasEnded = endedLocalText.Length > 0;
        StatusText = statusText;
        StatusToolTip = statusToolTip;
        StatusIsMissing = statusIsMissing;
        CanDeleteTranscript = canDeleteTranscript;
        DeleteFileToolTip = deleteFileToolTip;
        PowerShellText = powerShellText;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
        OpenTranscriptCommand = new RelayCommand(openTranscript);
        OpenFolderCommand = new RelayCommand(openFolder);
        CopyPathCommand = new AsyncRelayCommand(copyPath);
        RemoveFromHistoryCommand = new RelayCommand(removeFromHistory);
        RemoveAndDeleteFileCommand = new RelayCommand(removeAndDeleteFile, () => CanDeleteTranscript);
    }

    public string Id { get; }

    public string CustomerTag { get; }

    public string SessionName { get; }

    public string TranscriptFileName { get; }

    public string TranscriptPath { get; }

    public string StartedLocalText { get; }

    public string EndedLocalText { get; }

    public bool HasEnded { get; }

    public string StatusText { get; }

    public string? StatusToolTip { get; }

    public bool StatusIsMissing { get; }

    public bool CanDeleteTranscript { get; }

    public string DeleteFileToolTip { get; }

    public string PowerShellText { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
            _selectionChanged(Id, value);
        }
    }

    public IRelayCommand OpenTranscriptCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand CopyPathCommand { get; }

    public IRelayCommand RemoveFromHistoryCommand { get; }

    public IRelayCommand RemoveAndDeleteFileCommand { get; }
}
