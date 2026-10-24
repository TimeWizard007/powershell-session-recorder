using System.Collections.ObjectModel;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class SessionSetupViewModelTests
{
    [Fact]
    public void Draft_UpdatesPreviewValidationLanguageAndSettings()
    {
        using var directory = new TempSettingsDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.DirectoryPath, "settings.json"));
        var settings = new ApplicationSettings();
        var localization = new LocalizationService(LocalizationDirectory());
        var recentTags = new ObservableCollection<string>();
        var viewModel = new SessionSetupViewModel(
            localization,
            settings,
            store,
            recentTags,
            _ => { });

        Assert.Equal("Customer TAG is required.", viewModel.CustomerTagError);
        Assert.Equal("Session name is required.", viewModel.SessionNameError);
        Assert.Equal("Enter a customer TAG and session name to preview the transcript filename.", viewModel.ProposedFileName);
        Assert.Equal(OperatingSystem.IsWindows() ? 2 : 1, viewModel.PowerShellChoices.Count);

        viewModel.CustomerTag = "CONTOSO";
        viewModel.SessionName = "Mailbox repair";
        viewModel.IncludeDateAndTime = false;

        Assert.False(viewModel.HasCustomerTagError);
        Assert.False(viewModel.HasSessionNameError);
        Assert.Matches(@"^CONTOSO_Mailbox_repair_[A-F0-9]{8}\.txt$", viewModel.ProposedFileName);

        viewModel.IncludeDateAndTime = true;
        Assert.Matches(
            @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_CONTOSO_Mailbox_repair_[A-F0-9]{8}\.txt$",
            viewModel.ProposedFileName);
        Assert.Equal("Standard user", viewModel.PreviewPrivilege);

        viewModel.RunAsAdministrator = true;
        Assert.Equal(
            OperatingSystem.IsWindows() ? "Administrator (requested, not confirmed)" : "Administrator",
            viewModel.PreviewPrivilege);
        viewModel.RunAsAdministrator = false;

        localization.SetLanguage("pl-PL");

        Assert.Equal("Użytkownik standardowy", viewModel.PreviewPrivilege);
        viewModel.RunAsAdministrator = true;
        Assert.Equal(
            OperatingSystem.IsWindows() ? "Administrator (żądany, niepotwierdzony)" : "Administrator",
            viewModel.PreviewPrivilege);
        viewModel.CustomerTag = " ";
        Assert.Equal("TAG klienta jest wymagany.", viewModel.CustomerTagError);

        viewModel.CustomerTag = "CONTOSO";
        viewModel.LogDirectory = directory.DirectoryPath;
        viewModel.RememberCustomerTag();

        var loaded = store.Load();
        Assert.Equal("pl-PL", loaded.Language);
        Assert.Equal(directory.DirectoryPath, loaded.LastLogDirectory);
        Assert.True(loaded.IncludeDateAndTime);
        Assert.Contains("CONTOSO", loaded.RecentCustomerTags);
        Assert.Contains("CONTOSO", recentTags);
        Assert.DoesNotContain("runAsAdministrator", File.ReadAllText(store.FilePath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_PersistsTheSelectedLanguage()
    {
        using var directory = new TempSettingsDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.DirectoryPath, "settings.json"));
        var localization = new LocalizationService(LocalizationDirectory());
        var viewModel = new MainWindowViewModel(localization, new ApplicationSettings(), store);

        viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "pl-PL");

        Assert.Equal("pl-PL", store.Load().Language);
        Assert.Equal("Zmieniono język interfejsu.", viewModel.StatusMessage);
        Assert.NotNull(viewModel.SelectedLanguage);
        Assert.Equal("Polski", viewModel.SelectedLanguage.DisplayName);
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
            DirectoryPath = Directory.CreateTempSubdirectory("psr-viewmodel-").FullName;
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
