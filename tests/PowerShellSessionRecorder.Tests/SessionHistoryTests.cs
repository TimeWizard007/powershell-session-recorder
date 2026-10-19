using System.Globalization;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class SessionHistoryTests
{
    [Fact]
    public void Append_StoresAConfirmedSessionWithoutTranscriptContents()
    {
        using var directory = new TempDirectory();
        var transcript = Path.Combine(directory.Path, "HOWE_HISTORY-TEST-001.txt");
        File.WriteAllText(transcript, "SuperSecretToken\nGet-Date\n");
        var started = new DateTimeOffset(2026, 10, 18, 18, 4, 0, TimeSpan.Zero);
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));

        store.Append(SessionHistoryEntry.CreateConfirmed(Configuration("HOWE", "HISTORY-TEST-001", transcript), transcript, started));

        var loaded = store.Load();
        var entry = Assert.Single(loaded.Sessions);
        Assert.Equal(HistoryLoadKind.Loaded, loaded.Kind);
        Assert.Equal("HOWE", entry.CustomerTag);
        Assert.Equal("HISTORY-TEST-001", entry.SessionName);
        Assert.Equal("HOWE_HISTORY-TEST-001.txt", entry.TranscriptFileName);
        Assert.Equal(Path.GetFullPath(transcript), entry.TranscriptPath);
        Assert.Equal(started, entry.StartedUtc);
        Assert.Equal(PowerShellVersion.PowerShell7, entry.PowerShellVersion);
        Assert.Equal(SessionHistoryStatus.RecordingConfirmed, entry.RecordingStatus);
        Assert.Null(entry.EndedUtc);
        Assert.True(Guid.TryParse(entry.Id, out _));
        var json = File.ReadAllText(store.FilePath);
        Assert.DoesNotContain("SuperSecretToken", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Date", json, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveHistoryDisabled_DoesNotCreateAnEntry()
    {
        using var fixture = new HistoryRecordingFixture();
        fixture.ViewModel.SaveSessionInHistory = false;
        fixture.Service.Result = new SessionRecordingResult(SessionRecordingStatus.Started, fixture.TranscriptPath);

        await fixture.ViewModel.StartSessionCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.Service.Calls);
        Assert.Equal(HistoryLoadKind.Missing, fixture.History.Load().Kind);
        Assert.False(File.Exists(fixture.History.FilePath));
        Assert.Contains(fixture.TranscriptPath, fixture.Statuses.Last(), StringComparison.Ordinal);
        Assert.DoesNotContain("could not be added", fixture.Statuses.Last(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnconfirmedRecording_IsNotIndexed()
    {
        using var fixture = new HistoryRecordingFixture();
        fixture.Service.Result = new SessionRecordingResult(SessionRecordingStatus.Unconfirmed, fixture.TranscriptPath);

        await fixture.ViewModel.StartSessionCommand.ExecuteAsync(null);

        Assert.Equal(HistoryLoadKind.Missing, fixture.History.Load().Kind);
    }

    [Fact]
    public async Task HistoryWriteFailure_KeepsTheRecordingSuccess()
    {
        using var directory = new TempDirectory();
        var statuses = new List<string>();
        var service = new SessionSetupRecordingViewModelTests.FakeRecordingService
        {
            Result = new SessionRecordingResult(SessionRecordingStatus.Started, "/tmp/HOWE_HISTORY-TEST-001.txt")
        };
        var viewModel = CreateSession(directory.Path, statuses, service, new ThrowingHistoryStore());
        viewModel.CustomerTag = "HOWE";
        viewModel.SessionName = "HISTORY-TEST-001";
        viewModel.LogDirectory = directory.Path;
        viewModel.SaveSessionInHistory = true;

        await viewModel.StartSessionCommand.ExecuteAsync(null);

        Assert.Equal(1, service.Calls);
        Assert.Contains("Recording started. Transcript: /tmp/HOWE_HISTORY-TEST-001.txt", statuses.Last(), StringComparison.Ordinal);
        Assert.Contains("could not be added to History", statuses.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentAppends_KeepEverySession()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entries = Enumerable.Range(0, 20)
            .Select(index => Entry("HOWE", "Same name", Path.Combine(directory.Path, $"session-{index}.txt"), DateTimeOffset.UtcNow.AddMinutes(index)))
            .ToList();

        await Task.WhenAll(entries.Select(entry => Task.Run(() => store.Append(entry))));

        var loaded = new JsonHistoryStore(store.FilePath).Load();
        Assert.Equal(20, loaded.Sessions.Count);
        Assert.Equal(entries.Select(entry => entry.Id).OrderBy(id => id), loaded.Sessions.Select(entry => entry.Id).OrderBy(id => id));
    }

    [Fact]
    public void DuplicateTagAndName_StayDistinct()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "HISTORY-TEST-001", Path.Combine(directory.Path, "one.txt"), DateTimeOffset.UnixEpoch));
        store.Append(Entry("HOWE", "HISTORY-TEST-001", Path.Combine(directory.Path, "two.txt"), DateTimeOffset.UnixEpoch.AddMinutes(1)));

        var loaded = store.Load();
        Assert.Equal(2, loaded.Sessions.Count);
        Assert.NotEqual(loaded.Sessions[0].Id, loaded.Sessions[1].Id);
        Assert.NotEqual(loaded.Sessions[0].TranscriptPath, loaded.Sessions[1].TranscriptPath);
    }

    [Fact]
    public void DuplicateId_DoesNotReplaceTheExistingRecord()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entry = Entry("HOWE", "One", Path.Combine(directory.Path, "one.txt"), DateTimeOffset.UnixEpoch);
        store.Append(entry);
        var before = File.ReadAllBytes(store.FilePath);

        var duplicate = Entry("HOWE", "Two", Path.Combine(directory.Path, "two.txt"), DateTimeOffset.UnixEpoch.AddMinutes(1));
        duplicate.Id = entry.Id;
        Assert.Throws<InvalidOperationException>(() => store.Append(duplicate));

        Assert.Equal(before, File.ReadAllBytes(store.FilePath));
        Assert.Equal("One", Assert.Single(store.Load().Sessions).SessionName);
    }

    [Fact]
    public void FailedReplace_LeavesThePreviousHistoryIntact()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Kept", Path.Combine(directory.Path, "kept.txt"), DateTimeOffset.UnixEpoch));
        Directory.CreateDirectory(store.FilePath + ".tmp");

        Assert.ThrowsAny<IOException>(() =>
            store.Append(Entry("HOWE", "New", Path.Combine(directory.Path, "new.txt"), DateTimeOffset.UnixEpoch.AddMinutes(1))));

        Assert.Equal("Kept", Assert.Single(new JsonHistoryStore(store.FilePath).Load().Sessions).SessionName);
    }

    [Fact]
    public void Reload_SurvivesANewStoreInstance()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "history.json");
        var entry = Entry("HOWE", "HISTORY-TEST-001", Path.Combine(directory.Path, "session.txt"), DateTimeOffset.UnixEpoch);
        new JsonHistoryStore(path).Append(entry);

        var loaded = new JsonHistoryStore(path).Load();

        Assert.Equal(entry.Id, Assert.Single(loaded.Sessions).Id);
        Assert.Equal(entry.TranscriptPath, loaded.Sessions[0].TranscriptPath);
    }

    [Fact]
    public void MalformedJson_IsPreservedAndDoesNotBlockALaterEntry()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "history.json");
        File.WriteAllText(path, "{ not json");
        var store = new JsonHistoryStore(path);

        var loaded = store.Load();

        Assert.Equal(HistoryLoadKind.Quarantined, loaded.Kind);
        Assert.False(File.Exists(path));
        Assert.NotNull(store.LastQuarantinePath);
        Assert.Equal("{ not json", File.ReadAllText(store.LastQuarantinePath));

        store.Append(Entry("HOWE", "After", Path.Combine(directory.Path, "after.txt"), DateTimeOffset.UnixEpoch));
        Assert.Equal("After", Assert.Single(new JsonHistoryStore(path).Load().Sessions).SessionName);
        Assert.Equal("{ not json", File.ReadAllText(store.LastQuarantinePath));
    }

    [Fact]
    public void UnsupportedSchema_IsNotOverwritten()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "history.json");
        File.WriteAllText(path, """{"schemaVersion":99,"sessions":[]}""");
        var before = File.ReadAllBytes(path);
        var store = new JsonHistoryStore(path);

        Assert.Equal(HistoryLoadKind.UnsupportedSchema, store.Load().Kind);
        Assert.Throws<IOException>(() =>
            store.Append(Entry("HOWE", "Nope", Path.Combine(directory.Path, "nope.txt"), DateTimeOffset.UnixEpoch)));

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void MissingTranscript_IsReportedWithoutChangingTheRecord()
    {
        using var directory = new TempDirectory();
        var transcript = Path.Combine(directory.Path, "gone.txt");
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Gone", transcript, DateTimeOffset.UnixEpoch));
        var viewModel = CreateHistory(store);

        Assert.Equal("Transcript missing", Assert.Single(viewModel.Entries).StatusText);
        Assert.Equal(SessionHistoryStatus.RecordingConfirmed, Assert.Single(store.Load().Sessions).RecordingStatus);
        Assert.Equal(SessionHistoryStatus.TranscriptMissing, new TranscriptStatusProbe().Observe(transcript).Status);
    }

    [Fact]
    public void Footer_MarksTheSessionFinishedAndStoresTheEndTime()
    {
        using var directory = new TempDirectory();
        var transcript = Path.Combine(directory.Path, "done.txt");
        File.WriteAllText(transcript, new string('x', 20_000) + "\nPowerShell transcript end\nEnd time: 20261018184000\n");
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Done", transcript, DateTimeOffset.UnixEpoch));
        var viewModel = CreateHistory(store);

        var row = Assert.Single(viewModel.Entries);
        Assert.Equal("Finished", row.StatusText);
        Assert.True(row.HasEnded);
        var stored = Assert.Single(new JsonHistoryStore(store.FilePath).Load().Sessions);
        Assert.Equal(SessionHistoryStatus.Finished, stored.RecordingStatus);
        var expected = new DateTimeOffset(DateTime.SpecifyKind(
            new DateTime(2026, 10, 18, 18, 40, 0),
            DateTimeKind.Local)).ToUniversalTime();
        Assert.Equal(expected, stored.EndedUtc);
    }

    [Fact]
    public void MarkerOutsideTheTail_IsNotTreatedAsFinished()
    {
        using var directory = new TempDirectory();
        var transcript = Path.Combine(directory.Path, "open.txt");
        File.WriteAllText(transcript, "PowerShell transcript end\n" + new string('x', 20_000));

        var observation = new TranscriptStatusProbe().Observe(transcript);

        Assert.Equal(SessionHistoryStatus.RecordingConfirmed, observation.Status);
        Assert.Null(observation.EndedUtc);
    }

    [Fact]
    public void Query_FiltersByTagNameFilenameAndDateAndSortsNewestFirst()
    {
        var older = Entry("HOWE", "Alpha", "/tmp/alpha.txt", UtcFromLocal(2026, 10, 1, 9));
        var newer = Entry("NORTH", "HISTORY-TEST-001", "/tmp/HISTORY-TEST-001.txt", UtcFromLocal(2026, 10, 18, 15));
        var middle = Entry("HOWE", "Beta", "/tmp/beta.txt", UtcFromLocal(2026, 10, 18, 8));
        var entries = new[] { older, middle, newer };

        var byTag = SessionHistoryQuery.Apply(entries, null, "howe", null, null);
        Assert.Equal([middle.Id, older.Id], byTag.Select(entry => entry.Id));

        var byName = SessionHistoryQuery.Apply(entries, "history-test-001", null, null, null);
        Assert.Equal(newer.Id, Assert.Single(byName).Id);

        var byFile = SessionHistoryQuery.Apply(entries, "ALPHA.TXT", null, null, null);
        Assert.Equal(older.Id, Assert.Single(byFile).Id);

        var byDate = SessionHistoryQuery.Apply(entries, null, null, new DateTime(2026, 10, 18), new DateTime(2026, 10, 18));
        Assert.Equal([newer.Id, middle.Id], byDate.Select(entry => entry.Id));

        var sorted = SessionHistoryQuery.Apply(entries, null, null, null, null);
        Assert.Equal([newer.Id, middle.Id, older.Id], sorted.Select(entry => entry.Id));
    }

    [Fact]
    public void HistoryPage_DistinguishesEmptyAndNoMatchingResults()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var empty = CreateHistory(store);
        Assert.True(empty.ShowEmpty);
        Assert.False(empty.ShowNoMatches);
        Assert.False(empty.ShowList);

        store.Append(Entry("HOWE", "HISTORY-TEST-001", Path.Combine(directory.Path, "one.txt"), DateTimeOffset.UnixEpoch));
        var viewModel = CreateHistory(store);
        viewModel.SearchText = "missing-name";

        Assert.False(viewModel.ShowEmpty);
        Assert.True(viewModel.ShowNoMatches);
        Assert.False(viewModel.ShowList);

        viewModel.ClearFiltersCommand.Execute(null);
        Assert.True(viewModel.ShowList);
        Assert.Equal("", viewModel.SearchText);
    }

    [Fact]
    public void HistoryPage_FiltersTagAndDate()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "One", Path.Combine(directory.Path, "one.txt"), UtcFromLocal(2026, 10, 1, 10)));
        store.Append(Entry("NORTH", "Two", Path.Combine(directory.Path, "two.txt"), UtcFromLocal(2026, 10, 18, 10)));
        var viewModel = CreateHistory(store);

        viewModel.SelectedTag = viewModel.TagFilters.Single(option => option.Tag == "NORTH");
        Assert.Equal("Two", Assert.Single(viewModel.Entries).SessionName);

        viewModel.ClearFiltersCommand.Execute(null);
        viewModel.FromDate = new DateTime(2026, 10, 18);
        viewModel.ToDate = new DateTime(2026, 10, 18);
        Assert.Equal("Two", Assert.Single(viewModel.Entries).SessionName);
    }

    [Fact]
    public async Task CopyPath_CopiesTheAbsoluteTranscriptPath()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var transcript = Path.Combine(directory.Path, "copy.txt");
        store.Append(Entry("HOWE", "Copy", transcript, DateTimeOffset.UnixEpoch));
        var clipboard = new FakeClipboard();
        var statuses = new List<string>();
        var viewModel = CreateHistory(store, statuses, clipboard: clipboard);

        await Assert.Single(viewModel.Entries).CopyPathCommand.ExecuteAsync(null);

        Assert.Equal(Path.GetFullPath(transcript), clipboard.Text);
        Assert.Equal("Copied the transcript path.", statuses.Last());
    }

    [Theory]
    [InlineData(TranscriptOpenResult.Missing, "The transcript file is missing.")]
    [InlineData(TranscriptOpenResult.AccessDenied, "Access to the transcript was denied.")]
    [InlineData(TranscriptOpenResult.InvalidPath, "The transcript path is not valid.")]
    [InlineData(TranscriptOpenResult.Failed, "The transcript location could not be opened.")]
    public void OpenTranscript_ReportsLocalizedErrors(TranscriptOpenResult result, string expected)
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Open", Path.Combine(directory.Path, "open.txt"), DateTimeOffset.UnixEpoch));
        var statuses = new List<string>();
        var opener = new FakeOpener { FileResult = result };
        var viewModel = CreateHistory(store, statuses, opener);

        Assert.Single(viewModel.Entries).OpenTranscriptCommand.Execute(null);

        Assert.Equal(expected, statuses.Last());
        Assert.Equal(1, opener.FileCalls);
        Assert.Equal(0, opener.FolderCalls);
    }

    [Fact]
    public void OpenFolder_ReportsAPolishFailure()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Folder", Path.Combine(directory.Path, "folder.txt"), DateTimeOffset.UnixEpoch));
        var localization = new LocalizationService(LocalizationDirectory());
        localization.SetLanguage("pl-PL");
        var statuses = new List<string>();
        var viewModel = CreateHistory(store, statuses, new FakeOpener { FolderResult = TranscriptOpenResult.AccessDenied }, localization);

        Assert.Single(viewModel.Entries).OpenFolderCommand.Execute(null);

        Assert.Equal("Odmowa dostępu do transkrypcji.", statuses.Last());
    }

    [Fact]
    public void StatusLabels_DoNotClaimTheSessionIsActive()
    {
        var localization = new LocalizationService(LocalizationDirectory());
        foreach (var key in new[]
                 {
                     L.HistoryStatusRecordingConfirmed,
                     L.HistoryStatusFinished,
                     L.HistoryStatusUnknown,
                     L.HistoryStatusMissing
                 })
        {
            Assert.DoesNotContain("Active", localization.Get(key), StringComparison.Ordinal);
            localization.SetLanguage("pl-PL");
            Assert.DoesNotContain("Aktyw", localization.Get(key), StringComparison.OrdinalIgnoreCase);
            localization.SetLanguage("en-US");
        }
    }

    [Fact]
    public void PolishCatalog_UsesTheHistoryPhrases()
    {
        var localization = new LocalizationService(LocalizationDirectory());
        localization.SetLanguage("pl-PL");

        Assert.Equal("Potwierdzono rejestrowanie przy uruchomieniu", localization.Get(L.HistoryStatusRecordingConfirmed));
        Assert.Equal("Zakończona", localization.Get(L.HistoryStatusFinished));
        Assert.Equal("Brak transkrypcji", localization.Get(L.HistoryStatusMissing));
        Assert.Contains("nie są importowane", localization.Get(L.HistoryEmptyBody), StringComparison.Ordinal);
        Assert.Contains("Brak pasujących sesji", localization.Get(L.HistoryNoMatchesTitle), StringComparison.Ordinal);
        Assert.DoesNotContain("Active", localization.Get(L.HistoryStatusRecordingConfirmed), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinuxHistoryFile_IsLimitedToTheCurrentUser()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Mode", Path.Combine(directory.Path, "mode.txt"), DateTimeOffset.UnixEpoch));

        AssertOwnerOnly(store.FilePath);
        AssertOwnerOnly(store.FilePath + ".lock");
    }

    [Fact]
    public void UnreadableTranscript_IsUnknown()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var directory = new TempDirectory();
        var transcript = Path.Combine(directory.Path, "secret.txt");
        File.WriteAllText(transcript, "PowerShell transcript end\n");
        File.SetUnixFileMode(transcript, UnixFileMode.None);

        try
        {
            Assert.Equal(SessionHistoryStatus.Unknown, new TranscriptStatusProbe().Observe(transcript).Status);
            Assert.Equal(TranscriptOpenResult.AccessDenied, new TranscriptLocationOpener().TryOpenFile(transcript));
        }
        finally
        {
            File.SetUnixFileMode(transcript, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void Opener_RejectsAMissingFileAndAnInvalidPathWithoutLaunching()
    {
        using var directory = new TempDirectory();
        var opener = new TranscriptLocationOpener();
        var missing = Path.Combine(directory.Path, "missing-dir", "missing.txt");

        Assert.Equal(TranscriptOpenResult.Missing, opener.TryOpenFile(missing));
        Assert.Equal(TranscriptOpenResult.Missing, opener.TryOpenFolder(missing));
        Assert.Equal(TranscriptOpenResult.InvalidPath, opener.TryOpenFile(" "));
        Assert.Equal(TranscriptOpenResult.InvalidPath, opener.TryOpenFolder("bad\0path"));
    }

    [Fact]
    public void ExistingSettings_AreNotRewrittenWhenHistoryIsAdded()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(settingsPath, """
            {
              "schemaVersion": 1,
              "language": "pl-PL",
              "theme": "Dark",
              "lastLogDirectory": "/tmp",
              "lastPowerShellVersion": "PowerShell7",
              "includeDateAndTime": false,
              "saveSessionInHistory": true,
              "recentCustomerTags": [ "HOWE" ],
              "checkForUpdatesAutomatically": true,
              "lastUpdateCheckUtc": "2026-10-18T19:25:09+00:00",
              "lastKnownStableVersion": "",
              "lastKnownReleaseUrl": ""
            }
            """);
        var before = File.ReadAllBytes(settingsPath);
        var settings = new JsonSettingsStore(settingsPath).Load();
        var history = new JsonHistoryStore(JsonHistoryStore.GetFilePath(settingsPath));
        history.Append(Entry("HOWE", "Kept", Path.Combine(directory.Path, "kept.txt"), DateTimeOffset.UnixEpoch));

        Assert.Equal(before, File.ReadAllBytes(settingsPath));
        Assert.Equal("pl-PL", settings.Language);
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal("/tmp", settings.LastLogDirectory);
        Assert.Equal(PowerShellVersion.PowerShell7, settings.LastPowerShellVersion);
        Assert.True(settings.CheckForUpdatesAutomatically);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 19, 25, 9, TimeSpan.Zero), settings.LastUpdateCheckUtc);
        Assert.False(File.Exists(Path.Combine(directory.Path, "kept.txt")));
        Assert.NotEqual(settingsPath, history.FilePath);
    }

    [Fact]
    public void OpeningTheWindow_DoesNotCreateHistoryForAnOlderSettingsFile()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(settingsPath, """
            {
              "schemaVersion": 1,
              "language": "en-US",
              "lastLogDirectory": "/tmp",
              "lastPowerShellVersion": "PowerShell7",
              "includeDateAndTime": false,
              "saveSessionInHistory": true,
              "recentCustomerTags": [ "HOWE" ]
            }
            """);
        var before = File.ReadAllBytes(settingsPath);
        var store = new JsonSettingsStore(settingsPath);
        var localization = new LocalizationService(LocalizationDirectory());
        _ = new MainWindowViewModel(localization, store.Load(), store);

        Assert.Equal(before, File.ReadAllBytes(settingsPath));
        Assert.False(File.Exists(JsonHistoryStore.GetFilePath(settingsPath)));
        Assert.Equal("", new SessionSetupViewModel(
            localization,
            store.Load(),
            store,
            new System.Collections.ObjectModel.ObservableCollection<string>(),
            _ => { }).CustomerTag);
    }

    [Fact]
    public void LocalDisplayTime_IsDerivedFromUtc()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var started = new DateTimeOffset(2026, 10, 18, 12, 30, 0, TimeSpan.Zero);
        store.Append(Entry("HOWE", "Clock", Path.Combine(directory.Path, "clock.txt"), started));
        var viewModel = CreateHistory(store);

        var expected = started.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        Assert.Equal(expected, Assert.Single(viewModel.Entries).StartedLocalText);
    }

    private static void AssertOwnerOnly(string path)
    {
        if (!OperatingSystem.IsLinux())
            return;

        var mode = File.GetUnixFileMode(path);
        var shared = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        Assert.Equal(UnixFileMode.None, mode & shared);
        Assert.True(mode.HasFlag(UnixFileMode.UserRead));
        Assert.True(mode.HasFlag(UnixFileMode.UserWrite));
    }

    private static DateTimeOffset UtcFromLocal(int year, int month, int day, int hour)
        => new(DateTime.SpecifyKind(new DateTime(year, month, day, hour, 0, 0), DateTimeKind.Local));

    private static SessionConfiguration Configuration(string tag, string name, string transcript)
        => new()
        {
            CustomerTag = tag,
            SessionName = name,
            LogDirectory = Path.GetDirectoryName(transcript) ?? "",
            PowerShellVersion = PowerShellVersion.PowerShell7,
            SaveSessionInHistory = true
        };

    private static SessionHistoryEntry Entry(string tag, string name, string transcript, DateTimeOffset started)
    {
        var path = Path.GetFullPath(transcript);
        return new SessionHistoryEntry
        {
            Id = Guid.NewGuid().ToString("D"),
            CustomerTag = tag,
            SessionName = name,
            TranscriptFileName = Path.GetFileName(path),
            TranscriptPath = path,
            StartedUtc = started.ToUniversalTime(),
            PowerShellVersion = PowerShellVersion.PowerShell7,
            RecordingStatus = SessionHistoryStatus.RecordingConfirmed
        };
    }

    private static HistoryViewModel CreateHistory(
        JsonHistoryStore store,
        List<string>? statuses = null,
        FakeOpener? opener = null,
        LocalizationService? localization = null,
        FakeClipboard? clipboard = null)
    {
        localization ??= new LocalizationService(LocalizationDirectory());
        return new HistoryViewModel(
            localization,
            store,
            opener ?? new FakeOpener(),
            clipboard ?? new FakeClipboard(),
            message => statuses?.Add(message));
    }

    private static SessionSetupViewModel CreateSession(
        string directory,
        List<string> statuses,
        ISessionRecordingService recording,
        ISessionHistoryStore history)
    {
        var localization = new LocalizationService(LocalizationDirectory());
        return new SessionSetupViewModel(
            localization,
            new ApplicationSettings(),
            new JsonSettingsStore(Path.Combine(directory, "settings.json")),
            new System.Collections.ObjectModel.ObservableCollection<string>(),
            statuses.Add,
            recording,
            history: history);
    }

    private static string LocalizationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src/PowerShellSessionRecorder.App/Localization");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the localization catalogs.");
    }

    private sealed class HistoryRecordingFixture : IDisposable
    {
        public HistoryRecordingFixture()
        {
            Directory = new TempDirectory();
            TranscriptPath = Path.Combine(Directory.Path, "HOWE_HISTORY-TEST-001.txt");
            History = new JsonHistoryStore(Path.Combine(Directory.Path, "history.json"));
            Service = new SessionSetupRecordingViewModelTests.FakeRecordingService();
            ViewModel = CreateSession(Directory.Path, Statuses, Service, History);
            ViewModel.CustomerTag = "HOWE";
            ViewModel.SessionName = "HISTORY-TEST-001";
            ViewModel.LogDirectory = Directory.Path;
            ViewModel.SaveSessionInHistory = true;
        }

        public TempDirectory Directory { get; }

        public string TranscriptPath { get; }

        public JsonHistoryStore History { get; }

        public SessionSetupRecordingViewModelTests.FakeRecordingService Service { get; }

        public SessionSetupViewModel ViewModel { get; }

        public List<string> Statuses { get; } = [];

        public void Dispose() => Directory.Dispose();
    }

    private sealed class ThrowingHistoryStore : ISessionHistoryStore
    {
        public HistoryCatalog Load() => HistoryCatalog.Missing();

        public void Append(SessionHistoryEntry entry) => throw new IOException("History store is not writable.");

        public void RecordCompletion(string id, DateTimeOffset endedUtc) => throw new IOException("History store is not writable.");
    }

    private sealed class FakeOpener : ITranscriptLocationOpener
    {
        public TranscriptOpenResult FileResult { get; set; } = TranscriptOpenResult.Opened;

        public TranscriptOpenResult FolderResult { get; set; } = TranscriptOpenResult.Opened;

        public int FileCalls { get; private set; }

        public int FolderCalls { get; private set; }

        public TranscriptOpenResult TryOpenFile(string? path)
        {
            FileCalls++;
            return FileResult;
        }

        public TranscriptOpenResult TryOpenFolder(string? path)
        {
            FolderCalls++;
            return FolderResult;
        }
    }

    private sealed class FakeClipboard : ITextClipboard
    {
        public string? Text { get; private set; }

        public Task<bool> TrySetTextAsync(string text)
        {
            Text = text;
            return Task.FromResult(true);
        }
    }
}
