using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.App.Views;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var store = new JsonSettingsStore(AppDataPaths.GetSettingsFilePath());
        var settings = store.Load();
        var localization = new LocalizationService(AppDataPaths.GetLocalizationDirectory());
        localization.SetLanguage(settings.Language);
        if (!string.Equals(settings.Language, localization.Language, StringComparison.Ordinal))
        {
            settings.Language = localization.Language;
            TrySave(store, settings);
        }

        LocalizationService.SetCurrent(localization);
        var recording = new SessionRecordingService(
            new SystemExecutableProbe(),
            new SystemProcessRunner(),
            new FileTranscriptWatcher(),
            new BootstrapFileStore(BootstrapPaths.GetDirectory()));
        var themes = new AvaloniaThemeController();
        themes.Apply(settings.Theme);
        var mainViewModel = new MainWindowViewModel(
            localization,
            settings,
            store,
            recording,
            themes,
            new ExternalLinkOpener());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += (_, _) => mainViewModel.Flush();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void TrySave(JsonSettingsStore store, ApplicationSettings settings)
    {
        try
        {
            store.Save(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
