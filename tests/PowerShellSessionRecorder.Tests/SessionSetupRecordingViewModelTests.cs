using System.Collections.ObjectModel;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class SessionSetupRecordingViewModelTests
{
    [Fact]
    public void CanStartSession_IsFalseWhenTheConfigurationIsInvalid()
    {
        using var fixture = new RecordingFixture();
        fixture.ViewModel.LogDirectory = fixture.Directory;
        fixture.ViewModel.CustomerTag = "";
        fixture.ViewModel.SessionName = "Diagnostyka VPN";

        Assert.False(fixture.ViewModel.CanStartSession);
        Assert.Equal("Enter a customer TAG and a session name before starting.", fixture.ViewModel.StartSessionHint);
    }

    [Fact]
    public void CanStartSession_IsFalseWhenElevationIsRequested()
    {
        using var fixture = new RecordingFixture();
        Fill(fixture);
        fixture.ViewModel.RunAsAdministrator = true;

        Assert.False(fixture.ViewModel.CanStartSession);
        Assert.Contains("elevation", fixture.ViewModel.StartSessionHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanStartSession_IsFalseWhenTheTerminalIsMissing()
    {
        using var fixture = new RecordingFixture();
        fixture.Service.TerminalAvailable = false;
        Fill(fixture);

        if (OperatingSystem.IsWindows())
        {
            Assert.True(fixture.ViewModel.CanStartSession);
            Assert.DoesNotContain("GNOME Terminal", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
            Assert.Contains("PowerShell window", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
            return;
        }

        Assert.False(fixture.ViewModel.CanStartSession);
        Assert.Contains("GNOME Terminal", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
    }

    [Fact]
    public void CanStartSession_IsFalseWhenTerminalAppIsMissing()
    {
        using var fixture = new RecordingFixture(RecordingHost.Mac);
        fixture.Service.TerminalAvailable = false;
        Fill(fixture);

        Assert.False(fixture.ViewModel.CanStartSession);
        Assert.Equal("Terminal.app was not found.", fixture.ViewModel.StartSessionHint);
        Assert.True(fixture.ViewModel.ShowMacTerminalMissing);
        Assert.False(fixture.ViewModel.ShowLinuxTerminalInstall);
        Assert.DoesNotContain("GNOME Terminal", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo apt", fixture.ViewModel.StartSessionHint, StringComparison.Ordinal);
    }

    [Fact]
    public void CanStartSession_DescribesTerminalAppWhenTheMacHostIsReady()
    {
        using var fixture = new RecordingFixture(RecordingHost.Mac);
        Fill(fixture);

        Assert.True(fixture.ViewModel.CanStartSession);
        Assert.Equal(
            "Terminal.app will open PowerShell 7 with transcript recording already started.",
            fixture.ViewModel.StartSessionHint);
        Assert.False(fixture.ViewModel.ShowLinuxTerminalInstall);
        Assert.False(fixture.ViewModel.ShowMacTerminalMissing);
        Assert.False(fixture.ViewModel.ShowElevationOption);
    }

    [Fact]
    public void CanStartSession_IsFalseWhenTheRecordingServiceIsMissing()
    {
        using var directory = new TempDirectory();
        var statuses = new List<string>();
        var viewModel = CreateViewModel(directory.Path, statuses, recordingService: null);
        viewModel.CustomerTag = "HOWE";
        viewModel.SessionName = "Diagnostyka VPN";
        viewModel.LogDirectory = directory.Path;

        Assert.False(viewModel.CanStartSession);
        Assert.Equal("The recording runtime is not available.", viewModel.StartSessionHint);
    }

    [Fact]
    public void CanStartSession_IsTrueForAValidPowerShell7Configuration()
    {
        using var fixture = new RecordingFixture();
        Fill(fixture);

        Assert.True(fixture.ViewModel.CanStartSession);
        Assert.Matches("[A-F0-9]{8}", fixture.ViewModel.ProposedFileName);
        Assert.DoesNotMatch("[A-F0-9]{16}", fixture.ViewModel.ProposedFileName);
    }

    [Fact]
    public void LogDirectory_ExplainsThatAMissingFolderWillBeCreated()
    {
        using var fixture = new RecordingFixture();
        fixture.ViewModel.LogDirectory = Path.Combine(fixture.Directory, "missing");

        Assert.True(fixture.ViewModel.HasLogDirectoryHint);
        Assert.False(fixture.ViewModel.HasLogDirectoryError);
        Assert.Contains("will be created", fixture.ViewModel.LogDirectoryHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartSession_DoesNotCallTheServiceWhenTheConfigurationIsInvalid()
    {
        using var fixture = new RecordingFixture();
        fixture.ViewModel.LogDirectory = fixture.Directory;

        Assert.False(fixture.ViewModel.StartSessionCommand.CanExecute(null));
        fixture.ViewModel.StartSessionCommand.Execute(null);

        Assert.Equal(0, fixture.Service.Calls);
    }

    [Fact]
    public async Task StartSession_ReportsAPolishConfirmation()
    {
        using var fixture = new RecordingFixture();
        Fill(fixture);
        fixture.Localization.SetLanguage("pl-PL");
        fixture.Service.Result = new SessionRecordingResult(SessionRecordingStatus.Started, "/tmp/HOWE_Diagnostyka_VPN_ABC.txt");

        await fixture.ViewModel.StartSessionCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.Service.Calls);
        Assert.Contains("Rejestrowanie zostało uruchomione.", fixture.Statuses.Last(), StringComparison.Ordinal);
        Assert.Contains("/tmp/HOWE_Diagnostyka_VPN_ABC.txt", fixture.Statuses.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartSession_AllowsTwoIndependentLaunches()
    {
        using var fixture = new RecordingFixture();
        Fill(fixture);
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.OnStart = async () =>
        {
            Interlocked.Increment(ref entered);
            await release.Task;
        };

        var first = fixture.ViewModel.StartSessionCommand.ExecuteAsync(null);
        var second = fixture.ViewModel.StartSessionCommand.ExecuteAsync(null);
        var completed = SpinWait.SpinUntil(() => entered == 2, TimeSpan.FromSeconds(5));
        release.TrySetResult();
        await first;
        await second;

        Assert.True(completed);
        Assert.Equal(2, fixture.Service.Calls);
    }

    private static void Fill(RecordingFixture fixture)
    {
        fixture.ViewModel.CustomerTag = "HOWE";
        fixture.ViewModel.SessionName = "Diagnostyka VPN";
        fixture.ViewModel.LogDirectory = fixture.Directory;
        fixture.ViewModel.SelectedPowerShellVersion = PowerShellVersion.PowerShell7;
    }

    private static SessionSetupViewModel CreateViewModel(
        string directory,
        List<string> statuses,
        ISessionRecordingService? recordingService)
    {
        var localization = new LocalizationService(LocalizationDirectory());
        var store = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
        return new SessionSetupViewModel(
            localization,
            new ApplicationSettings(),
            store,
            new ObservableCollection<string>(),
            statuses.Add,
            recordingService);
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

    public sealed class RecordingFixture : IDisposable
    {
        public RecordingFixture(RecordingHost? host = null)
        {
            Directory = System.IO.Directory.CreateTempSubdirectory("psr-vm-").FullName;
            Localization = new LocalizationService(LocalizationDirectory());
            Service = new FakeRecordingService();
            Host = host ?? (OperatingSystem.IsWindows() ? RecordingHost.Windows : RecordingHost.Linux);
            var store = new JsonSettingsStore(Path.Combine(Directory, "settings.json"));
            ViewModel = new SessionSetupViewModel(
                Localization,
                new ApplicationSettings(),
                store,
                new ObservableCollection<string>(),
                Statuses.Add,
                Service,
                host: Host);
        }

        public RecordingHost Host { get; }

        public string Directory { get; }

        public LocalizationService Localization { get; }

        public FakeRecordingService Service { get; }

        public SessionSetupViewModel ViewModel { get; }

        public List<string> Statuses { get; } = [];

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    public sealed class FakeRecordingService : ISessionRecordingService
    {
        public bool PowerShellAvailable { get; set; } = true;

        public bool TerminalAvailable { get; set; } = true;

        public int Calls { get; private set; }

        public Func<Task>? OnStart { get; set; }

        public SessionRecordingResult Result { get; set; } = new(SessionRecordingStatus.Started, "/tmp/transcript.txt");

        public bool PlatformSupported { get; set; } = true;

        public int RefreshCount { get; private set; }

        public SessionRuntimeCapabilities Capabilities => new(PowerShellAvailable, TerminalAvailable, PlatformSupported);

        public void RefreshDependencies() => RefreshCount++;

        public async Task<SessionRecordingResult> StartAsync(
            SessionConfiguration configuration,
            string? language,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (OnStart is not null)
                await OnStart();

            return Result;
        }
    }
}
