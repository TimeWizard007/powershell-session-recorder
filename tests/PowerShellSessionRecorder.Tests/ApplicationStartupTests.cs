using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class ApplicationStartupTests
{
    [Fact]
    public void FirstLaunch_WithoutSettings_ConstructsDefaults()
    {
        using var directory = new TempSettingsDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.DirectoryPath, "settings.json"));

        var viewModel = Start(store, store.Load());

        Assert.Equal("en-US", viewModel.SelectedLanguage?.Code);
        Assert.Equal("", viewModel.Session.LogDirectory);
        Assert.True(viewModel.Session.IncludeDateAndTime);
        Assert.True(viewModel.Session.SaveSessionInHistory);
        Assert.Empty(viewModel.RecentCustomerTags);
        Assert.Equal("Customer TAG is required.", viewModel.Session.CustomerTagError);
        Assert.Equal(
            "Enter a customer TAG and session name to preview the transcript filename.",
            viewModel.Session.ProposedFileName);
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void Startup_WithSavedPolishSettings_RestoresTheDraftWithoutThrowing()
    {
        using var directory = new TempSettingsDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.DirectoryPath, "settings.json"));
        var original = """
            {
              "schemaVersion": 1,
              "language": "pl-PL",
              "lastLogDirectory": "/tmp",
              "lastPowerShellVersion": "PowerShell7",
              "includeDateAndTime": false,
              "saveSessionInHistory": true,
              "recentCustomerTags": ["HOWE"]
            }
            """;
        File.WriteAllText(store.FilePath, original);

        var viewModel = Start(store, store.Load());

        Assert.Equal(original, File.ReadAllText(store.FilePath));
        Assert.Equal("pl-PL", viewModel.SelectedLanguage?.Code);
        Assert.Equal("/tmp", viewModel.Session.LogDirectory);
        Assert.Equal("/tmp", viewModel.Session.PreviewLogDirectory);
        Assert.False(viewModel.Session.IncludeDateAndTime);
        Assert.True(viewModel.Session.SaveSessionInHistory);
        Assert.Equal(["HOWE"], viewModel.RecentCustomerTags);
        Assert.Equal("TAG klienta jest wymagany.", viewModel.Session.CustomerTagError);
        Assert.Equal("PowerShell 7", viewModel.Session.PreviewPowerShellVersion);

        viewModel.Session.CustomerTag = viewModel.RecentCustomerTags[0];
        viewModel.Session.SessionName = "Restart";

        Assert.Matches(@"^HOWE_Restart_[A-F0-9]{8}\.txt$", viewModel.Session.ProposedFileName);
    }

    [Fact]
    public void Startup_WithSavedEnglishSettings_RestoresTheLogDirectory()
    {
        using var directory = new TempSettingsDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.DirectoryPath, "settings.json"));
        File.WriteAllText(
            store.FilePath,
            """
            {
              "schemaVersion": 1,
              "language": "en-US",
              "lastLogDirectory": "/var/log/sessions",
              "lastPowerShellVersion": "PowerShell7",
              "includeDateAndTime": true,
              "saveSessionInHistory": false,
              "recentCustomerTags": ["CONTOSO", "FABRIKAM"]
            }
            """);

        var viewModel = Start(store, store.Load());

        Assert.Equal("en-US", viewModel.SelectedLanguage?.Code);
        Assert.Equal("/var/log/sessions", viewModel.Session.LogDirectory);
        Assert.True(viewModel.Session.IncludeDateAndTime);
        Assert.False(viewModel.Session.SaveSessionInHistory);
        Assert.Equal(["CONTOSO", "FABRIKAM"], viewModel.RecentCustomerTags);
        Assert.Equal("Customer TAG is required.", viewModel.Session.CustomerTagError);
        Assert.Equal("English", viewModel.SelectedLanguage?.DisplayName);

        viewModel.Session.CustomerTag = "CONTOSO";
        viewModel.Session.SessionName = "Mailbox repair";
        Assert.Matches(
            @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_CONTOSO_Mailbox_repair_[A-F0-9]{8}\.txt$",
            viewModel.Session.ProposedFileName);
    }

    [Fact]
    public void Restart_AfterChangingSettings_LoadsTheUpdatedDraft()
    {
        using var directory = new TempSettingsDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.DirectoryPath, "settings.json"));
        var first = Start(store, new ApplicationSettings());
        first.Session.LogDirectory = "/tmp";
        first.Session.IncludeDateAndTime = false;
        first.Session.SaveSessionInHistory = true;
        first.Session.CustomerTag = "HOWE";
        first.Session.RememberCustomerTag();
        first.SelectedLanguage = first.Languages.Single(language => language.Code == "pl-PL");

        var reloaded = store.Load();
        var second = Start(store, reloaded);

        Assert.Equal("pl-PL", second.SelectedLanguage?.Code);
        Assert.Equal("/tmp", second.Session.LogDirectory);
        Assert.False(second.Session.IncludeDateAndTime);
        Assert.True(second.Session.SaveSessionInHistory);
        Assert.Equal(["HOWE"], second.RecentCustomerTags);

        second.Session.CustomerTag = "HOWE";
        second.Session.SessionName = "Follow-up";
        Assert.False(second.Session.HasCustomerTagError);
        Assert.Matches(@"^HOWE_Follow-up_[A-F0-9]{8}\.txt$", second.Session.ProposedFileName);
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

    private sealed class TempSettingsDirectory : IDisposable
    {
        public TempSettingsDirectory()
        {
            DirectoryPath = Directory.CreateTempSubdirectory("psr-startup-").FullName;
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
