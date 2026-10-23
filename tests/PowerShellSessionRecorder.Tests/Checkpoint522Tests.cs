using System.Diagnostics;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint522Tests
{
    private static readonly string[] UserDataNames =
    [
        "settings.json",
        "settings.invalid.json",
        "customers.json",
        "history.json",
        "customers.json.lock",
        "history.json.lock"
    ];

    [Fact]
    public void FreshWindowsAndLinuxProfiles_StartWithAnEmptyCatalogAndHistory()
    {
        using var profile = new TempProfile();
        var windowsSettings = SettingsPathResolver.GetSettingsFilePath(
            SettingsPlatform.Windows,
            homeDirectory: null,
            xdgConfigHome: null,
            windowsApplicationData: Path.Combine(profile.Path, "Local"));
        var linuxSettings = SettingsPathResolver.GetSettingsFilePath(
            SettingsPlatform.Linux,
            homeDirectory: profile.Path,
            xdgConfigHome: null,
            windowsApplicationData: null);

        Assert.EndsWith(
            Path.Combine("Local", "PowerShellSessionRecorder", "settings.json"),
            windowsSettings,
            StringComparison.Ordinal);
        Assert.EndsWith(
            Path.Combine(".config", "powershell-session-recorder", "settings.json"),
            linuxSettings,
            StringComparison.Ordinal);
        Assert.NotEqual(
            Path.GetDirectoryName(windowsSettings),
            Path.Combine(profile.Path, "Local", "Programs", "PowerShellSessionRecorder"),
            StringComparer.Ordinal);

        AssertFreshProfile(windowsSettings);
        AssertFreshProfile(linuxSettings);
    }

    [Fact]
    public void ExistingCustomerCatalog_IsShownAndLeftUnchanged()
    {
        using var profile = new TempProfile();
        var directory = Path.Combine(profile.Path, "PowerShellSessionRecorder");
        Directory.CreateDirectory(directory);
        var customers = """
            {
              "schemaVersion": 1,
              "customers": [
                {
                  "id": "bdf5416e-1dc7-4288-a6b6-33e1b949eff2",
                  "tag": "HOWE",
                  "displayName": "HOWE",
                  "createdUtc": "2026-10-19T01:58:56.7407483+00:00"
                }
              ]
            }
            """;
        var history = """
            {
              "schemaVersion": 1,
              "sessions": [
                {
                  "id": "2a9f29e2-3eae-40c3-955f-c72b0a5772af",
                  "customerTag": "HOWE",
                  "sessionName": "Earlier test",
                  "transcriptFileName": "HOWE_Earlier_test.txt",
                  "transcriptPath": "/tmp/does-not-exist/HOWE_Earlier_test.txt",
                  "startedUtc": "2026-10-19T01:59:31.4198828+00:00",
                  "powerShellVersion": "PowerShell7",
                  "recordingStatus": "Finished",
                  "endedUtc": "2026-10-19T01:59:59+00:00"
                }
              ]
            }
            """;
        var customerPath = Path.Combine(directory, "customers.json");
        var historyPath = Path.Combine(directory, "history.json");
        File.WriteAllText(customerPath, customers);
        File.WriteAllText(historyPath, history);

        var store = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
        var viewModel = Start(store, store.Load());

        Assert.Equal("HOWE", Assert.Single(viewModel.Session.Customers.Choices).Tag);
        Assert.Null(viewModel.Session.Customers.SelectedCustomer);
        Assert.Equal("", viewModel.Session.CustomerTag);
        Assert.False(viewModel.Session.Customers.ShowEmpty);
        Assert.Equal(1, viewModel.History.IndexedSessionCount);
        Assert.Equal(customers, File.ReadAllText(customerPath));
        Assert.Equal(history, File.ReadAllText(historyPath));
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void PackagesAndResources_DoNotShipSampleCustomers()
    {
        var root = RepositoryRoot();
        foreach (var catalog in Directory.GetFiles(Path.Combine(root, "src/PowerShellSessionRecorder.App/Localization"), "*.json"))
            Assert.DoesNotContain("HOWE", File.ReadAllText(catalog), StringComparison.Ordinal);

        foreach (var project in new[] { "src/PowerShellSessionRecorder.App", "src/PowerShellSessionRecorder.Core" })
        {
            var shipped = Directory.GetFiles(Path.Combine(root, project), "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .ToArray();
            Assert.DoesNotContain(shipped, name => UserDataNames.Contains(name, StringComparer.OrdinalIgnoreCase));
        }

        var installer = File.ReadAllText(Path.Combine(root, "packaging/windows/PowerShellSessionRecorder.iss"));
        Assert.Contains("Excludes: \"*.pdb,settings.json,customers.json,history.json\"", installer, StringComparison.Ordinal);
        foreach (var script in new[]
        {
            "packaging/reject-user-data.sh",
            "packaging/ubuntu/build-deb.sh",
            "packaging/linux/build-tarball.sh",
            "packaging/windows/publish-win-x64.sh"
        })
        {
            Assert.Contains("reject_user_data", File.ReadAllText(Path.Combine(root, script)), StringComparison.Ordinal);
        }

        AssertPackageMembers(
            Path.Combine(root, "artifacts/powershell-session-recorder_0.1.0~beta.2-1_amd64.deb"),
            "dpkg-deb",
            "-c");
        AssertPackageMembers(
            Path.Combine(root, "artifacts/powershell-session-recorder-0.1.0-beta.2-linux-x64.tar.gz"),
            "tar",
            "-tzf");

        var windows = Path.Combine(root, "artifacts/win-x64");
        if (Directory.Exists(windows))
            AssertNoUserData(Directory.EnumerateFiles(windows, "*", SearchOption.AllDirectories).Select(Path.GetFileName));
    }

    private static void AssertFreshProfile(string settingsPath)
    {
        var store = new JsonSettingsStore(settingsPath);
        var loaded = store.LoadDetailed();
        Assert.Equal(SettingsLoadStatus.Missing, loaded.Status);
        var settings = loaded.Settings;
        Assert.Equal("en-US", settings.Language);
        Assert.Equal("", settings.LastLogDirectory);
        Assert.Equal(PowerShellVersion.PowerShell7, settings.LastPowerShellVersion);
        Assert.True(settings.IncludeDateAndTime);
        Assert.True(settings.SaveSessionInHistory);
        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.True(settings.CheckForUpdatesAutomatically);
        Assert.Equal(UpdateChannel.Stable, settings.UpdateChannel);
        Assert.Empty(settings.RecentCustomerTags);

        var viewModel = Start(store, settings);
        Assert.Empty(viewModel.RecentCustomerTags);
        Assert.Empty(viewModel.Session.Customers.Choices);
        Assert.Null(viewModel.Session.Customers.SelectedCustomer);
        Assert.Equal("", viewModel.Session.CustomerTag);
        Assert.True(viewModel.Session.Customers.ShowEmpty);
        Assert.Equal(0, viewModel.History.IndexedSessionCount);
        Assert.True(viewModel.History.ShowEmpty);
        Assert.Equal("en-US", viewModel.SelectedLanguage?.Code);

        var directory = Path.GetDirectoryName(settingsPath)!;
        if (Directory.Exists(directory))
        {
            Assert.Empty(Directory.EnumerateFiles(directory));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.txt", SearchOption.AllDirectories));
        }

        Assert.False(File.Exists(settingsPath));
        Assert.False(File.Exists(JsonCustomerCatalog.GetFilePath(settingsPath)));
        Assert.False(File.Exists(JsonHistoryStore.GetFilePath(settingsPath)));
    }

    private static void AssertPackageMembers(string packagePath, string program, string argument)
    {
        if (!File.Exists(packagePath))
            return;

        var start = new ProcessStartInfo
        {
            FileName = program,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(argument);
        start.ArgumentList.Add(packagePath);
        using var process = Process.Start(start);
        Assert.NotNull(process);
        var listing = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        AssertNoUserData(listing.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFileName));
        Assert.DoesNotContain("HOWE", listing, StringComparison.Ordinal);
    }

    private static void AssertNoUserData(IEnumerable<string?> names)
    {
        var present = names
            .Where(name => name is not null && UserDataNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        Assert.Empty(present);
    }

    private static MainWindowViewModel Start(JsonSettingsStore store, ApplicationSettings settings)
    {
        var localization = new LocalizationService(LocalizationDirectory());
        localization.SetLanguage(settings.Language);
        return new MainWindowViewModel(localization, settings, store);
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

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PowerShellSessionRecorder.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed class TempProfile : IDisposable
    {
        public TempProfile()
        {
            Path = Directory.CreateTempSubdirectory("psr-clean-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
