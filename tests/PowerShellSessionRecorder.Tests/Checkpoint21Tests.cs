using System.Collections.ObjectModel;
using System.Reflection;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint21Tests
{
    [Fact]
    public void Load_KeepsAnOlderSettingsFileWithoutATheme()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "language": "pl-PL",
              "lastLogDirectory": "/tmp",
              "lastPowerShellVersion": "PowerShell7",
              "includeDateAndTime": false,
              "saveSessionInHistory": true,
              "recentCustomerTags": [ "HOWE" ]
            }
            """);
        var before = File.ReadAllBytes(path);

        var loaded = new JsonSettingsStore(path).LoadDetailed();

        Assert.Equal(SettingsLoadStatus.Loaded, loaded.Status);
        Assert.Equal(AppTheme.System, loaded.Settings.Theme);
        Assert.Equal(["HOWE"], loaded.Settings.RecentCustomerTags);
        Assert.Equal("pl-PL", loaded.Settings.Language);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Save_RoundTripsTheSelectedTheme()
    {
        using var directory = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.Path, "settings.json"));
        store.Save(new ApplicationSettings { Theme = AppTheme.Dark });

        var loaded = store.Load();

        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Contains("\"Dark\"", File.ReadAllText(store.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void NewSession_DoesNotSelectARememberedCustomerTag()
    {
        using var directory = new TempDirectory();
        var settings = new ApplicationSettings();
        settings.RememberCustomerTag("HOWE");
        var localization = new LocalizationService(LocalizationDirectory());
        var viewModel = new SessionSetupViewModel(
            localization,
            settings,
            new JsonSettingsStore(Path.Combine(directory.Path, "settings.json")),
            new ObservableCollection<string>(settings.RecentCustomerTags),
            _ => { });

        Assert.Equal("", viewModel.CustomerTag);
        Assert.Contains("HOWE", settings.RecentCustomerTags);
    }

    [Fact]
    public void RefreshDependencies_ReevaluatesAMissingComponent()
    {
        using var fixture = new SessionSetupRecordingViewModelTests.RecordingFixture();
        fixture.ViewModel.CustomerTag = "HOWE";
        fixture.ViewModel.SessionName = "Diagnostyka VPN";
        fixture.ViewModel.LogDirectory = fixture.Directory;
        fixture.Service.PowerShellAvailable = false;

        Assert.False(fixture.ViewModel.CanStartSession);
        Assert.True(fixture.ViewModel.ShowPowerShellMissing);
        Assert.Contains("PowerShell 7", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);

        fixture.Service.PowerShellAvailable = true;
        fixture.ViewModel.RefreshDependenciesCommand.Execute(null);

        Assert.Equal(1, fixture.Service.RefreshCount);
        Assert.True(fixture.ViewModel.CanStartSession);
        Assert.False(fixture.ViewModel.ShowDependencyNotice);
    }

    [Fact]
    public void MissingGnomeTerminal_ShowsTheUbuntuInstallCommandWithoutRunningIt()
    {
        using var fixture = new SessionSetupRecordingViewModelTests.RecordingFixture();
        fixture.Service.TerminalAvailable = false;

        Assert.True(fixture.ViewModel.ShowTerminalMissing);
        Assert.Equal(RecordingPlatform.UbuntuTerminalInstallCommand, fixture.ViewModel.UbuntuTerminalInstallCommand);
        Assert.Equal("sudo apt install gnome-terminal", fixture.ViewModel.UbuntuTerminalInstallCommand);
        Assert.Contains("GNOME Terminal", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedPlatform_DoesNotSuggestTheUbuntuPackageCommandAsSufficient()
    {
        using var fixture = new SessionSetupRecordingViewModelTests.RecordingFixture();
        fixture.Service.PlatformSupported = false;
        fixture.Service.TerminalAvailable = false;

        Assert.True(fixture.ViewModel.ShowUnsupportedPlatform);
        Assert.False(fixture.ViewModel.ShowTerminalMissing);
        Assert.Contains("Ubuntu", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
        Assert.Contains("macOS", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutVersion_MatchesTheAssemblyInformationalVersion()
    {
        var informational = typeof(ProductVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        Assert.False(string.IsNullOrWhiteSpace(informational));
        var published = informational!.Split('+', 2)[0];
        Assert.Equal(published, ProductVersion.Current);
        Assert.Equal(RecordingPlatform.RepositoryUrl, "https://github.com/TimeWizard007/powershell-session-recorder");
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
}
