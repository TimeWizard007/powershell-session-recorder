using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint41Tests
{
    [Fact]
    public void Customers_CanBeCreatedRenamedRemovedAndReloaded()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "customers.json");
        var created = new DateTimeOffset(2026, 10, 19, 1, 2, 3, TimeSpan.Zero);
        var catalog = new JsonCustomerCatalog(path);
        var added = catalog.Add("  TEST  ", "  North office  ", created);

        Assert.Equal(CustomerEditError.None, added.Error);
        Assert.Equal("TEST", added.Customer!.Tag);
        Assert.Equal("North office", added.Customer.DisplayName);

        var renamed = catalog.Update(added.Customer.Id, "TEST-RENAMED", "Renamed");
        Assert.Equal(CustomerEditError.None, renamed.Error);
        Assert.Equal(added.Customer.Id, renamed.Customer!.Id);
        Assert.Equal(created, renamed.Customer.CreatedUtc);

        var reloaded = new JsonCustomerCatalog(path).Load();
        var customer = Assert.Single(reloaded.Customers);
        Assert.Equal("TEST-RENAMED", customer.Tag);
        Assert.Equal("Renamed", customer.DisplayName);
        Assert.Equal(CustomerCatalogKind.Loaded, reloaded.Kind);

        Assert.Equal(CustomerEditError.None, catalog.Remove(customer.Id).Error);
        Assert.Empty(new JsonCustomerCatalog(path).Load().Customers);
        Assert.Equal(CustomerEditError.Missing, catalog.Remove(customer.Id).Error);
    }

    [Fact]
    public void Customers_RejectDuplicateAndInvalidTags()
    {
        using var directory = new TempDirectory();
        var catalog = new JsonCustomerCatalog(Path.Combine(directory.Path, "customers.json"));
        Assert.Equal(CustomerEditError.None, catalog.Add("HOWE", "", DateTimeOffset.UnixEpoch).Error);

        Assert.Equal(CustomerEditError.Duplicate, catalog.Add("howe", "Other", DateTimeOffset.UnixEpoch).Error);
        Assert.Equal(CustomerEditError.Required, catalog.Add("   ", null, DateTimeOffset.UnixEpoch).Error);
        Assert.Equal(CustomerEditError.Invalid, catalog.Add("...", null, DateTimeOffset.UnixEpoch).Error);
        Assert.Equal(CustomerEditError.Invalid, catalog.Add("///", null, DateTimeOffset.UnixEpoch).Error);
        Assert.Equal(CustomerEditError.DisplayNameInvalid, catalog.Add("OTHER", "bad\nname", DateTimeOffset.UnixEpoch).Error);
        Assert.Equal(CustomerEditError.DisplayNameInvalid, catalog.Add("OTHER", new string('a', 201), DateTimeOffset.UnixEpoch).Error);

        var only = Assert.Single(catalog.Load().Customers);
        Assert.Equal("HOWE", only.Tag);
        Assert.Equal(CustomerEditError.None, catalog.Update(only.Id, "howe", "Same spelling").Error);
        Assert.Equal("howe", catalog.Load().Customers.Single().Tag);
        Assert.Equal("Same spelling", catalog.Load().Customers.Single().DisplayName);
    }

    [Fact]
    public void Customers_DoNotImportHistoryAndDoNotChangeIt()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(settingsPath, """
            {
              "schemaVersion": 1,
              "language": "en-US",
              "recentCustomerTags": [ "HOWE" ]
            }
            """);
        var settingsBefore = File.ReadAllBytes(settingsPath);
        var transcript = FinishedTranscript(Path.Combine(directory.Path, "HOWE_old.txt"));
        var history = new JsonHistoryStore(JsonHistoryStore.GetFilePath(settingsPath));
        history.Append(Entry("HOWE", "Old", transcript, DateTimeOffset.UnixEpoch));
        var historyBefore = File.ReadAllBytes(history.FilePath);

        var catalog = new JsonCustomerCatalog(JsonCustomerCatalog.GetFilePath(settingsPath));
        Assert.Equal(CustomerCatalogKind.Missing, catalog.Load().Kind);
        Assert.False(File.Exists(catalog.FilePath));

        var added = catalog.Add("HOWE", null, DateTimeOffset.UnixEpoch);
        catalog.Update(added.Customer!.Id, "HOWE-RENAMED", "Kept");
        Assert.Equal(historyBefore, File.ReadAllBytes(history.FilePath));
        Assert.Equal("HOWE", history.Load().Sessions.Single().CustomerTag);
        Assert.Equal(transcript, history.Load().Sessions.Single().TranscriptPath);
        Assert.True(File.Exists(transcript));

        catalog.Remove(added.Customer.Id);
        Assert.Equal(historyBefore, File.ReadAllBytes(history.FilePath));
        Assert.True(File.Exists(transcript));
        Assert.Equal(settingsBefore, File.ReadAllBytes(settingsPath));
    }

    [Fact]
    public void NewSession_DoesNotSelectACustomerUntilTheUserChoosesOne()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        var catalog = new JsonCustomerCatalog(JsonCustomerCatalog.GetFilePath(settingsPath));
        catalog.Add("HOWE", "Recent", DateTimeOffset.UnixEpoch);
        catalog.Add("TEST", null, DateTimeOffset.UnixEpoch);
        var localization = new LocalizationService(LocalizationDirectory());
        var viewModel = new SessionSetupViewModel(
            localization,
            new ApplicationSettings { RecentCustomerTags = ["HOWE"] },
            new JsonSettingsStore(settingsPath),
            new ObservableCollection<string>(["HOWE"]),
            _ => { },
            customers: catalog);

        Assert.Null(viewModel.Customers.SelectedCustomer);
        Assert.Equal("", viewModel.CustomerTag);
        Assert.Equal("No customer is selected.", viewModel.Customers.SelectedTagText);
        Assert.True(viewModel.Customers.ShowEmpty is false);
        Assert.Equal(2, viewModel.Customers.Choices.Count);

        viewModel.Customers.BeginAddCommand.Execute(null);
        viewModel.Customers.NewTag = "HOWE";
        viewModel.Customers.SaveNewCommand.Execute(null);
        Assert.Equal("A customer with this TAG already exists.", viewModel.Customers.FormError);
        Assert.Null(viewModel.Customers.SelectedCustomer);

        viewModel.Customers.NewTag = "HOWE2";
        viewModel.Customers.NewDisplayName = "Second";
        viewModel.Customers.SaveNewCommand.Execute(null);
        Assert.Equal("HOWE2", viewModel.CustomerTag);
        Assert.Equal("HOWE2", viewModel.Customers.SelectedCustomer!.Tag);
        Assert.Equal("Selected TAG: HOWE2", viewModel.Customers.SelectedTagText);

        var howe = viewModel.Customers.Choices.Single(choice => choice.Tag == "HOWE");
        howe.RemoveCommand.Execute(null);
        Assert.Contains("History entries and transcript files are kept", viewModel.Customers.PendingRemovalBody);
        viewModel.Customers.ConfirmRemoveCommand.Execute(null);
        Assert.Equal("HOWE2", viewModel.CustomerTag);

        viewModel.Customers.SelectedCustomer.RemoveCommand.Execute(null);
        viewModel.Customers.ConfirmRemoveCommand.Execute(null);
        Assert.Null(viewModel.Customers.SelectedCustomer);
        Assert.Equal("", viewModel.CustomerTag);
        Assert.False(File.Exists(JsonHistoryStore.GetFilePath(settingsPath)));
    }

    [Fact]
    public void CustomerSearch_FiltersTheManageListWithoutClearingAnotherSelection()
    {
        using var directory = new TempDirectory();
        var catalog = new JsonCustomerCatalog(Path.Combine(directory.Path, "customers.json"));
        var viewModel = DirectoryFor(catalog);
        viewModel.BeginAddCommand.Execute(null);
        viewModel.NewTag = "HOWE";
        viewModel.SaveNewCommand.Execute(null);
        viewModel.BeginAddCommand.Execute(null);
        viewModel.NewTag = "TEST";
        viewModel.NewDisplayName = "North";
        viewModel.SaveNewCommand.Execute(null);
        Assert.Equal("TEST", viewModel.SelectedCustomer!.Tag);

        viewModel.SearchText = "north";
        Assert.Equal("TEST", Assert.Single(viewModel.VisibleCustomers).Tag);
        Assert.Equal("TEST", viewModel.SelectedCustomer.Tag);
        viewModel.SearchText = "missing";
        Assert.True(viewModel.ShowNoMatches);
        Assert.Equal("TEST", viewModel.SelectedCustomer.Tag);
    }

    [Fact]
    public void LinuxCustomerCatalog_IsOwnerOnlyAndRefusesSymlinks()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "customers.json");
        var catalog = new JsonCustomerCatalog(path);
        Assert.Equal(CustomerEditError.None, catalog.Add("TEST", null, DateTimeOffset.UnixEpoch).Error);

        AssertOwnerFile(path);
        AssertOwnerFile(path + ".lock");
        AssertOwnerDirectory(directory.Path);
        Assert.DoesNotContain(Directory.GetFiles(directory.Path), candidate => candidate.EndsWith(".tmp", StringComparison.Ordinal));

        var secretPath = Path.Combine(directory.Path, "secret.json");
        File.WriteAllText(secretPath, "secret-customer");
        var link = Path.Combine(directory.Path, "linked-customers.json");
        File.CreateSymbolicLink(link, secretPath);
        var linked = new JsonCustomerCatalog(link);
        Assert.Equal(CustomerCatalogKind.Unreadable, linked.Load().Kind);
        Assert.Equal(CustomerEditError.CatalogUnavailable, linked.Add("TAG", null, DateTimeOffset.UnixEpoch).Error);
        Assert.Equal("secret-customer", File.ReadAllText(secretPath));

        var linkedDirectory = Path.Combine(directory.Path, "linked-dir");
        Directory.CreateSymbolicLink(linkedDirectory, directory.Path);
        var throughDirectory = new JsonCustomerCatalog(Path.Combine(linkedDirectory, "customers.json"));
        Assert.Equal(CustomerCatalogKind.Unreadable, throughDirectory.Load().Kind);
        Assert.Equal("secret-customer", File.ReadAllText(secretPath));
    }

    [Fact]
    public void UnsupportedCustomerCatalog_IsLeftUntouched()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "customers.json");
        File.WriteAllText(path, """{"schemaVersion": 2, "customers": []}""");
        var before = File.ReadAllBytes(path);
        var catalog = new JsonCustomerCatalog(path);

        Assert.Equal(CustomerCatalogKind.UnsupportedSchema, catalog.Load().Kind);
        Assert.Equal(CustomerEditError.CatalogUnavailable, catalog.Add("TEST", null, DateTimeOffset.UnixEpoch).Error);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task ConcurrentCustomerAdds_KeepDistinctTags()
    {
        using var directory = new TempDirectory();
        var catalog = new JsonCustomerCatalog(Path.Combine(directory.Path, "customers.json"));
        await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
            Task.Run(() => catalog.Add("TAG-" + index, null, DateTimeOffset.UnixEpoch))));

        var loaded = new JsonCustomerCatalog(catalog.FilePath).Load();
        Assert.Equal(12, loaded.Customers.Count);
        Assert.Equal(12, loaded.Customers.Select(customer => customer.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void HistoryOnlyRemoval_KeepsTheTranscript()
    {
        using var directory = new TempDirectory();
        var transcript = UnfinishedTranscript(Path.Combine(directory.Path, "open.txt"));
        var neighbor = Path.Combine(directory.Path, "unrelated.txt");
        File.WriteAllText(neighbor, "keep");
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entry = Entry("HOWE", "Open", transcript, DateTimeOffset.UnixEpoch);
        store.Append(entry);

        var report = HistoryCleanup.Remove(store, [entry], deleteFiles: false);

        Assert.Equal(1, report.HistoryRemoved);
        Assert.Equal(0, report.FilesDeleted);
        Assert.False(report.DeletesFiles);
        Assert.Empty(store.Load().Sessions);
        Assert.True(File.Exists(transcript));
        Assert.Equal("keep", File.ReadAllText(neighbor));
    }

    [Fact]
    public void FinishedTranscript_CanBeDeletedWithItsHistoryEntry()
    {
        using var directory = new TempDirectory();
        var transcript = FinishedTranscript(Path.Combine(directory.Path, "done.txt"));
        var neighbor = Path.Combine(directory.Path, "unrelated.txt");
        File.WriteAllText(neighbor, "keep");
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entry = Entry("HOWE", "Done", transcript, DateTimeOffset.UnixEpoch);
        store.Append(entry);

        var report = HistoryCleanup.Remove(store, [entry], deleteFiles: true);

        Assert.Equal(1, report.HistoryRemoved);
        Assert.Equal(1, report.FilesDeleted);
        Assert.False(File.Exists(transcript));
        Assert.Equal("keep", File.ReadAllText(neighbor));
        Assert.Empty(store.Load().Sessions);
    }

    [Fact]
    public void UnconfirmedTranscript_IsNotDeletedAndStaysInHistory()
    {
        using var directory = new TempDirectory();
        var transcript = UnfinishedTranscript(Path.Combine(directory.Path, "open.txt"));
        var before = File.ReadAllBytes(transcript);
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entry = Entry("HOWE", "Open", transcript, DateTimeOffset.UnixEpoch);
        store.Append(entry);

        var report = HistoryCleanup.Remove(store, [entry], deleteFiles: true);

        Assert.Equal(0, report.HistoryRemoved);
        Assert.Equal(1, report.FilesPreserved);
        Assert.Equal(before, File.ReadAllBytes(transcript));
        Assert.Equal(entry.Id, store.Load().Sessions.Single().Id);
    }

    [Theory]
    [InlineData("symlink")]
    [InlineData("directory")]
    [InlineData("outside")]
    public void UnsafeTranscriptTargets_AreRefused(string kind)
    {
        using var directory = new TempDirectory();
        var neighbor = Path.Combine(directory.Path, "unrelated.txt");
        File.WriteAllText(neighbor, "keep");
        string target;
        if (kind == "directory")
        {
            target = Path.Combine(directory.Path, "not-a-file");
            Directory.CreateDirectory(target);
            target = Path.GetFullPath(target);
        }
        else if (kind == "symlink")
        {
            if (!OperatingSystem.IsLinux())
                return;

            var secret = Path.Combine(directory.Path, "secret.txt");
            File.WriteAllText(secret, "secret");
            target = Path.Combine(directory.Path, "link.txt");
            File.CreateSymbolicLink(target, secret);
            target = Path.GetFullPath(target);
        }
        else
        {
            var file = FinishedTranscript(Path.Combine(directory.Path, "done.txt"));
            target = Path.Combine(directory.Path, "nested", "..", Path.GetFileName(file));
            Assert.NotEqual(file, target);
            Assert.Equal(TranscriptDeleteStatus.Unsafe, TranscriptFileDeleter.TryDelete(target).Status);
            Assert.True(File.Exists(file));
            Assert.Equal("keep", File.ReadAllText(neighbor));
            return;
        }

        Assert.Equal(TranscriptDeleteStatus.Unsafe, TranscriptFileDeleter.TryDelete(target).Status);
        if (kind == "directory")
            Assert.True(Directory.Exists(target));
        else
            Assert.Equal("secret", File.ReadAllText(Path.Combine(directory.Path, "secret.txt")));
        Assert.Equal("keep", File.ReadAllText(neighbor));
    }

    [Fact]
    public void MarkerOutsideTheTail_DoesNotAllowDeletion()
    {
        using var directory = new TempDirectory();
        var transcript = Path.Combine(directory.Path, "large.txt");
        File.WriteAllText(transcript, "PowerShell transcript end\n" + new string('x', 20_000));
        var full = Path.GetFullPath(transcript);

        Assert.Equal(TranscriptDeleteStatus.NotFinished, TranscriptFileDeleter.TryDelete(full).Status);
        Assert.True(File.Exists(full));
    }

    [Fact]
    public void MissingTranscript_StillAllowsHistoryRemoval()
    {
        using var directory = new TempDirectory();
        var missing = Path.GetFullPath(Path.Combine(directory.Path, "missing.txt"));
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entry = Entry("HOWE", "Gone", missing, DateTimeOffset.UnixEpoch);
        store.Append(entry);

        var report = HistoryCleanup.Remove(store, [entry], deleteFiles: true);

        Assert.Equal(TranscriptDeleteStatus.Missing, TranscriptFileDeleter.TryDelete(missing).Status);
        Assert.Equal(1, report.HistoryRemoved);
        Assert.Equal(1, report.FilesAlreadyMissing);
        Assert.Equal(0, report.FilesDeleted);
        Assert.Empty(store.Load().Sessions);
    }

    [Fact]
    public void PermissionFailure_KeepsTheHistoryEntryAndTheFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var directory = new TempDirectory();
        var transcript = FinishedTranscript(Path.Combine(directory.Path, "locked.txt"));
        File.SetUnixFileMode(transcript, UnixFileMode.None);
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entry = Entry("HOWE", "Locked", transcript, DateTimeOffset.UnixEpoch);
        store.Append(entry);
        try
        {
            var report = HistoryCleanup.Remove(store, [entry], deleteFiles: true);
            Assert.Equal(0, report.HistoryRemoved);
            Assert.Equal(1, report.FilesRefused);
            Assert.True(File.Exists(transcript));
            Assert.Single(store.Load().Sessions);
        }
        finally
        {
            File.SetUnixFileMode(transcript, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void PartialBulkFailure_RemovesOnlySuccessfulEntries()
    {
        using var directory = new TempDirectory();
        var finished = FinishedTranscript(Path.Combine(directory.Path, "done.txt"));
        var unfinished = UnfinishedTranscript(Path.Combine(directory.Path, "open.txt"));
        var neighbor = Path.Combine(directory.Path, "unrelated.txt");
        File.WriteAllText(neighbor, "keep");
        var locked = FinishedTranscript(Path.Combine(directory.Path, "locked.txt"));
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(locked, UnixFileMode.None);

        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var done = Entry("HOWE", "Done", finished, DateTimeOffset.UnixEpoch);
        var open = Entry("HOWE", "Open", unfinished, DateTimeOffset.UnixEpoch.AddMinutes(1));
        var denied = Entry("HOWE", "Locked", locked, DateTimeOffset.UnixEpoch.AddMinutes(2));
        store.Append(done);
        store.Append(open);
        store.Append(denied);
        try
        {
            var report = HistoryCleanup.Remove(store, [done, open, denied], deleteFiles: true);
            Assert.Equal(1, report.FilesPreserved);
            if (OperatingSystem.IsLinux())
            {
                Assert.Equal(1, report.FilesDeleted);
                Assert.Equal(1, report.FilesRefused);
                Assert.Equal(1, report.HistoryRemoved);
                Assert.True(File.Exists(locked));
                Assert.Equal(["Locked", "Open"], store.Load().Sessions.Select(entry => entry.SessionName).OrderBy(name => name));
            }
            else
            {
                Assert.Equal(2, report.FilesDeleted);
                Assert.Equal(0, report.FilesRefused);
                Assert.Equal(2, report.HistoryRemoved);
                Assert.False(File.Exists(locked));
                Assert.Equal("Open", store.Load().Sessions.Single().SessionName);
            }

            Assert.False(File.Exists(finished));
            Assert.True(File.Exists(unfinished));
            Assert.Equal("keep", File.ReadAllText(neighbor));
        }
        finally
        {
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void HistoryWriteFailure_DoesNotHideADeletedFile()
    {
        using var directory = new TempDirectory();
        var transcript = FinishedTranscript(Path.Combine(directory.Path, "done.txt"));
        var inner = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var entry = Entry("HOWE", "Done", transcript, DateTimeOffset.UnixEpoch);
        inner.Append(entry);
        var store = new RemoveFailsStore(inner);

        var report = HistoryCleanup.Remove(store, [entry], deleteFiles: true);

        Assert.True(report.HistoryWriteFailed);
        Assert.Equal(1, report.FilesDeleted);
        Assert.Equal(0, report.HistoryRemoved);
        Assert.False(File.Exists(transcript));
        Assert.Equal(entry.Id, inner.Load().Sessions.Single().Id);
    }

    [Fact]
    public async Task ConcurrentHistoryRemoval_DoesNotDropUnrelatedEntries()
    {
        using var directory = new TempDirectory();
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        var original = Enumerable.Range(0, 10)
            .Select(index => Entry("HOWE", "Old-" + index, Path.Combine(directory.Path, $"old-{index}.txt"), DateTimeOffset.UnixEpoch.AddMinutes(index)))
            .ToList();
        foreach (var entry in original)
            store.Append(entry);
        var added = Enumerable.Range(0, 10)
            .Select(index => Entry("HOWE", "New-" + index, Path.Combine(directory.Path, $"new-{index}.txt"), DateTimeOffset.UnixEpoch.AddHours(1).AddMinutes(index)))
            .ToList();

        await Task.WhenAll(
            Task.Run(() => store.Remove(original.Take(5).Select(entry => entry.Id).ToArray())),
            Task.Run(() =>
            {
                foreach (var entry in added)
                    store.Append(entry);
            }));

        var ids = store.Load().Sessions.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(15, ids.Count);
        Assert.All(original.Skip(5), entry => Assert.Contains(entry.Id, ids));
        Assert.All(added, entry => Assert.Contains(entry.Id, ids));
        Assert.All(original.Take(5), entry => Assert.DoesNotContain(entry.Id, ids));
    }

    [Fact]
    public void FilteredSelection_DoesNotRemoveHiddenHistory()
    {
        using var directory = new TempDirectory();
        var keep = Path.Combine(directory.Path, "keep.txt");
        var drop = Path.Combine(directory.Path, "drop.txt");
        File.WriteAllText(keep, "keep");
        File.WriteAllText(drop, "drop");
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Keep", keep, DateTimeOffset.UnixEpoch));
        store.Append(Entry("OTHER", "Drop", drop, DateTimeOffset.UnixEpoch.AddMinutes(1)));
        var viewModel = HistoryFor(store);

        viewModel.SelectAllShownCommand.Execute(null);
        Assert.Equal(2, viewModel.SelectedCount);
        viewModel.SearchText = "OTHER";
        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Equal("Drop", Assert.Single(viewModel.Entries).SessionName);

        viewModel.AskRemoveSelectedHistoryCommand.Execute(null);
        Assert.Contains("Transcript files to delete: 0", viewModel.PendingCleanupSummary);
        Assert.Contains("Remove 1 history", viewModel.PendingCleanupSummary);
        viewModel.ConfirmCleanupCommand.Execute(null);

        Assert.Equal("Keep", store.Load().Sessions.Single().SessionName);
        Assert.Equal("keep", File.ReadAllText(keep));
        Assert.Equal("drop", File.ReadAllText(drop));
    }

    [Fact]
    public void BulkFileCleanup_ShowsCountsAndKeepsUnfinishedEntries()
    {
        using var directory = new TempDirectory();
        var finished = FinishedTranscript(Path.Combine(directory.Path, "done.txt"));
        var unfinished = UnfinishedTranscript(Path.Combine(directory.Path, "open.txt"));
        var store = new JsonHistoryStore(Path.Combine(directory.Path, "history.json"));
        store.Append(Entry("HOWE", "Done", finished, DateTimeOffset.UnixEpoch));
        store.Append(Entry("HOWE", "Open", unfinished, DateTimeOffset.UnixEpoch.AddMinutes(1)));
        var viewModel = HistoryFor(store);
        viewModel.SelectAllShownCommand.Execute(null);
        viewModel.AskRemoveSelectedFilesCommand.Execute(null);

        Assert.Contains("History entries: 2", viewModel.PendingCleanupSummary);
        Assert.Contains("Transcript files to delete: 1", viewModel.PendingCleanupSummary);
        Assert.Contains("not confirmed finished", viewModel.PendingCleanupSummary);
        Assert.Contains("does not prove", viewModel.PendingCleanupSummary);

        viewModel.ConfirmCleanupCommand.Execute(null);
        Assert.True(File.Exists(unfinished));
        Assert.False(File.Exists(finished));
        Assert.Equal("Open", store.Load().Sessions.Single().SessionName);
        Assert.Contains("deleted 1", viewModel.ActionMessage);
    }

    [Fact]
    public void CleanupCopy_IsAvailableInPolish()
    {
        var localization = new LocalizationService(LocalizationDirectory());
        localization.SetLanguage("pl-PL");
        var english = new LocalizationService(LocalizationDirectory());

        Assert.NotEqual(english.Get(L.CustomerEmptyBody), localization.Get(L.CustomerEmptyBody));
        Assert.NotEqual(english.Get(L.HistoryRemoveAndFile), localization.Get(L.HistoryRemoveAndFile));
        Assert.NotEqual(english.Get(L.HistoryCleanupFooterNote), localization.Get(L.HistoryCleanupFooterNote));
        Assert.Contains("Wybrany TAG", localization.Get(L.CustomerSelectedTag));
        Assert.Contains("nie dowodzi", localization.Get(L.HistoryCleanupFooterNote), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Usuń i skasuj transkrypcję", localization.Get(L.HistoryRemoveAndFile));
    }

    [Fact]
    public void WindowsFileInformation_UsesTheWin32FieldOrder()
    {
        var type = typeof(WindowsTranscriptDeletion.ByHandleFileInformation);
        Assert.Equal(28, Marshal.OffsetOf(type, nameof(WindowsTranscriptDeletion.ByHandleFileInformation.VolumeSerialNumber)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf(type, nameof(WindowsTranscriptDeletion.ByHandleFileInformation.FileSizeHigh)).ToInt32());
        Assert.Equal(36, Marshal.OffsetOf(type, nameof(WindowsTranscriptDeletion.ByHandleFileInformation.FileSizeLow)).ToInt32());
        Assert.Equal(40, Marshal.OffsetOf(type, nameof(WindowsTranscriptDeletion.ByHandleFileInformation.NumberOfLinks)).ToInt32());
        Assert.Equal(44, Marshal.OffsetOf(type, nameof(WindowsTranscriptDeletion.ByHandleFileInformation.FileIndexHigh)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf(type, nameof(WindowsTranscriptDeletion.ByHandleFileInformation.FileIndexLow)).ToInt32());
        Assert.Equal(52, Marshal.SizeOf(type));
        Assert.Equal(21, WindowsTranscriptDeletion.FileDispositionInfoExClass);
    }

    private static CustomerDirectoryViewModel DirectoryFor(JsonCustomerCatalog catalog)
    {
        var localization = new LocalizationService(LocalizationDirectory());
        return new CustomerDirectoryViewModel(localization, catalog, _ => { });
    }

    private static HistoryViewModel HistoryFor(JsonHistoryStore store)
    {
        var localization = new LocalizationService(LocalizationDirectory());
        return new HistoryViewModel(localization, store, new NoOpener(), new NoClipboard(), _ => { });
    }

    private static string FinishedTranscript(string path)
    {
        File.WriteAllText(path, "PowerShell transcript start\nPowerShell transcript end\nEnd time: 20261019030738\n");
        return Path.GetFullPath(path);
    }

    private static string UnfinishedTranscript(string path)
    {
        File.WriteAllText(path, "PowerShell transcript start\nGet-Date\n");
        return Path.GetFullPath(path);
    }

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

    private static void AssertOwnerFile(string path)
    {
        if (!OperatingSystem.IsLinux())
            return;

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    private static void AssertOwnerDirectory(string path)
    {
        if (!OperatingSystem.IsLinux())
            return;

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(path));
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

        throw new InvalidOperationException("Could not locate localization files.");
    }

    private sealed class RemoveFailsStore(JsonHistoryStore inner) : ISessionHistoryStore
    {
        public HistoryCatalog Load() => inner.Load();

        public void Append(SessionHistoryEntry entry) => inner.Append(entry);

        public void RecordCompletion(string id, DateTimeOffset endedUtc) => inner.RecordCompletion(id, endedUtc);

        public int Remove(IReadOnlyCollection<string> ids) => throw new IOException("History store is not writable.");
    }

    private sealed class NoOpener : ITranscriptLocationOpener
    {
        public TranscriptOpenResult TryOpenFile(string? path) => TranscriptOpenResult.Opened;

        public TranscriptOpenResult TryOpenFolder(string? path) => TranscriptOpenResult.Opened;
    }

    private sealed class NoClipboard : ITextClipboard
    {
        public Task<bool> TrySetTextAsync(string text) => Task.FromResult(true);
    }
}
