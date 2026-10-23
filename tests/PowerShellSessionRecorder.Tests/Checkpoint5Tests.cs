using System.Collections.ObjectModel;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint5Tests
{
    [Fact]
    public void EngineDiscovery_PrefersProgramFilesAndSkipsTheCurrentDirectory()
    {
        using var root = new TempDirectory();
        var programFiles = Path.Combine(root.Path, "Program Files");
        var installed = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
        var current = Path.Combine(root.Path, "cwd");
        var planted = Path.Combine(current, "pwsh.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        Directory.CreateDirectory(current);
        File.WriteAllText(installed, "");
        File.WriteAllText(planted, "");

        var found = PowerShellEngineDiscovery.DiscoverWindows(
            pathVariable: string.Join(Path.PathSeparator, ".", current, programFiles),
            programFiles,
            systemRoot: null,
            current,
            path => new EngineFileInspection(File.Exists(path), false));

        var powerShell7 = Assert.Single(found);
        Assert.Equal(PowerShellVersion.PowerShell7, powerShell7.Version);
        Assert.Equal(Path.GetFullPath(installed), powerShell7.Path);
    }

    [Fact]
    public void EngineDiscovery_UsesSystem32ForWindowsPowerShellAndSkipsReparsePoints()
    {
        using var root = new TempDirectory();
        var systemRoot = Path.Combine(root.Path, "Windows");
        var system32 = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var alias = Path.Combine(root.Path, "Program Files", "PowerShell", "7", "pwsh.exe");
        var pathCopy = Path.Combine(root.Path, "tools", "pwsh.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(system32)!);
        Directory.CreateDirectory(Path.GetDirectoryName(alias)!);
        Directory.CreateDirectory(Path.GetDirectoryName(pathCopy)!);
        File.WriteAllText(system32, "");
        File.WriteAllText(alias, "");
        File.WriteAllText(pathCopy, "");

        var found = PowerShellEngineDiscovery.DiscoverWindows(
            Path.GetDirectoryName(pathCopy),
            Path.Combine(root.Path, "Program Files"),
            systemRoot,
            currentDirectory: root.Path,
            path => new EngineFileInspection(File.Exists(path), path == Path.GetFullPath(alias)));

        Assert.Equal(2, found.Count);
        Assert.Equal(PowerShellVersion.PowerShell7, found[0].Version);
        Assert.Equal(Path.GetFullPath(pathCopy), found[0].Path);
        Assert.Equal(PowerShellVersion.WindowsPowerShell51, found[1].Version);
        Assert.Equal(Path.GetFullPath(system32), found[1].Path);
    }

    [Fact]
    public void WindowsArguments_KeepDataOutOfTheScriptAndTheShell()
    {
        var transcript = Path.Combine("C:", "logs", "'; Remove-Item -Recurse.txt");
        var arguments = RecordingBootstrap.BuildWindowsArguments(
            PowerShellVersion.WindowsPowerShell51,
            Path.Combine("C:", "Users", "admin"),
            Path.Combine("C:", "bootstrap.ps1"),
            transcript,
            "pl-PL",
            "PowerShell Session Recorder — HOWE");

        Assert.DoesNotContain("-Command", arguments);
        Assert.DoesNotContain("-EncodedCommand", arguments);
        Assert.DoesNotContain("-WorkingDirectory", arguments.Take(5));
        Assert.Equal("Bypass", arguments[arguments.ToList().IndexOf("-ExecutionPolicy") + 1]);
        Assert.Equal(transcript, arguments[arguments.ToList().IndexOf("-TranscriptPath") + 1]);
        Assert.DoesNotContain(transcript, RecordingBootstrap.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("HOWE", RecordingBootstrap.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", RecordingBootstrap.Script, StringComparison.Ordinal);

        var powerShell7 = RecordingBootstrap.BuildWindowsArguments(
            PowerShellVersion.PowerShell7,
            "/home/admin",
            "/tmp/bootstrap.ps1",
            "/tmp/transcript.txt",
            "en-US",
            "PowerShell Session Recorder");
        Assert.Contains("-WorkingDirectory", powerShell7.Take(8));
        var quoted = WindowsCommandLine.Join(arguments);
        Assert.Contains(WindowsCommandLine.Quote(transcript), quoted, StringComparison.Ordinal);
        Assert.DoesNotContain("cmd.exe", quoted, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("a\\", "\"a\\\\\"")]
    [InlineData("a\\\"b", "\"a\\\\\\\"b\"")]
    public void WindowsCommandLine_QuotesOneArgument(string value, string expected)
        => Assert.Equal(expected, WindowsCommandLine.Quote(value));

    [Fact]
    public void LaunchPlan_RejectsARelativeOrRenamedExecutable()
    {
        using var root = new TempDirectory();
        var executable = Path.GetFullPath(Path.Combine(root.Path, "pwsh.exe"));
        File.WriteAllText(executable, "");
        var nested = Path.Combine(root.Path, "nested");
        Directory.CreateDirectory(nested);
        var transcript = Path.Combine(nested, "HOWE_Session.txt");
        File.WriteAllText(transcript, "");

        Assert.True(WindowsLaunchPlanFactory.TryCreate(
            PowerShellVersion.PowerShell7,
            executable,
            root.Path,
            Path.Combine(root.Path, "bootstrap.ps1"),
            transcript,
            "en-US",
            "PowerShell Session Recorder",
            elevate: true,
            executableIsReparsePoint: false,
            out var plan));
        Assert.NotNull(plan);
        Assert.True(plan!.Elevate);
        Assert.DoesNotContain("-Command", plan.Arguments);
        Assert.Equal(WindowsCommandLine.Join(plan.Arguments), plan.QuotedArguments);

        Assert.False(WindowsLaunchPlanFactory.TryCreate(
            PowerShellVersion.PowerShell7,
            "pwsh.exe",
            root.Path,
            Path.Combine(root.Path, "bootstrap.ps1"),
            transcript,
            "en-US",
            "title",
            false,
            false,
            out _));
        Assert.False(WindowsLaunchPlanFactory.TryCreate(
            PowerShellVersion.WindowsPowerShell51,
            executable,
            root.Path,
            Path.Combine(root.Path, "bootstrap.ps1"),
            transcript,
            "en-US",
            "title",
            false,
            false,
            out _));
        Assert.False(WindowsLaunchPlanFactory.TryCreate(
            PowerShellVersion.PowerShell7,
            executable,
            root.Path,
            Path.Combine(root.Path, "bootstrap.ps1"),
            transcript,
            "en-US",
            "title",
            false,
            executableIsReparsePoint: true,
            out _));
    }

    [Fact]
    public async Task ElevationCancellation_DoesNotStartOrRecordHistory()
    {
        using var root = new TempDirectory();
        var executable = Path.GetFullPath(Path.Combine(root.Path, "pwsh.exe"));
        var transcript = Path.Combine(root.Path, "HOWE_Session.txt");
        File.WriteAllText(executable, "");
        File.WriteAllText(transcript, "");
        var launcher = new FakeWindowsLauncher { Result = WindowsSessionStart.Cancelled };
        var watcher = new CountingWatcher();
        var starter = new WindowsSessionStarter(launcher, watcher);
        var configuration = new SessionConfiguration
        {
            CustomerTag = "HOWE",
            SessionName = "Session",
            LogDirectory = root.Path,
            PowerShellVersion = PowerShellVersion.PowerShell7,
            RunAsAdministrator = true,
            SaveSessionInHistory = true,
            IncludeDateAndTime = false
        };

        var result = await starter.StartAsync(
            configuration,
            "en-US",
            executable,
            false,
            Path.Combine(root.Path, "bootstrap.ps1"),
            transcript,
            root.Path,
            CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.ElevationCancelled, result.Status);
        Assert.Null(result.TranscriptPath);
        Assert.False(File.Exists(transcript));
        Assert.Equal(0, watcher.Calls);
        Assert.True(launcher.Plan!.Elevate);
        Assert.Equal("runas-not-shell", launcher.Marker);

        var historyPath = Path.Combine(root.Path, "history.json");
        var history = new JsonHistoryStore(historyPath);
        var localization = new LocalizationService(LocalizationDirectory());
        var statuses = new List<string>();
        var viewModel = new SessionSetupViewModel(
            localization,
            new ApplicationSettings { LastLogDirectory = root.Path, SaveSessionInHistory = true },
            new JsonSettingsStore(Path.Combine(root.Path, "settings.json")),
            new ObservableCollection<string>(),
            statuses.Add,
            new CancelledRecordingService(),
            history: history);
        viewModel.CustomerTag = "HOWE";
        viewModel.SessionName = "Session";
        viewModel.LogDirectory = root.Path;
        await viewModel.StartSessionCommand.ExecuteAsync(null);

        Assert.Empty(history.Load().Sessions);
        Assert.Contains("canceled", statuses.Last(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnconfirmedWindowsLaunch_KeepsTheTranscriptOutOfHistory()
    {
        using var root = new TempDirectory();
        var executable = Path.GetFullPath(Path.Combine(root.Path, "powershell.exe"));
        var transcript = Path.Combine(root.Path, "keep.txt");
        File.WriteAllText(executable, "");
        File.WriteAllText(transcript, "reserved");
        var starter = new WindowsSessionStarter(
            new FakeWindowsLauncher { Result = WindowsSessionStart.Started },
            new CountingWatcher());

        var result = await starter.StartAsync(
            new SessionConfiguration
            {
                CustomerTag = "HOWE",
                SessionName = "Session",
                LogDirectory = root.Path,
                PowerShellVersion = PowerShellVersion.WindowsPowerShell51,
                IncludeDateAndTime = false
            },
            "en-US",
            executable,
            false,
            Path.Combine(root.Path, "bootstrap.ps1"),
            transcript,
            root.Path,
            CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.Unconfirmed, result.Status);
        Assert.Equal("reserved", File.ReadAllText(transcript));
    }

    [Fact]
    public void StartPolicy_AllowsWindowsElevationAndKeepsUbuntuRestricted()
    {
        var ready = new SessionRuntimeCapabilities(true, true, true, true, true);
        Assert.False(SessionStartPolicy.CanStart(false, ready, PowerShellVersion.PowerShell7, true, true, true, false));
        Assert.False(SessionStartPolicy.CanStart(false, new SessionRuntimeCapabilities(true, false), PowerShellVersion.PowerShell7, false, true, true, false));
        Assert.True(SessionStartPolicy.CanStart(true, ready, PowerShellVersion.PowerShell7, true, true, true, false));
        Assert.True(SessionStartPolicy.CanStart(true, ready, PowerShellVersion.WindowsPowerShell51, false, true, true, false));
        Assert.False(SessionStartPolicy.CanStart(true, new SessionRuntimeCapabilities(true, true, true, false, true), PowerShellVersion.WindowsPowerShell51, false, true, true, false));
    }

    [Theory]
    [InlineData(true, false, false, false, null, false, TranscriptDeleteStatus.Missing)]
    [InlineData(false, true, false, true, SessionHistoryStatus.Finished, false, TranscriptDeleteStatus.Unsafe)]
    [InlineData(false, false, true, false, SessionHistoryStatus.Finished, false, TranscriptDeleteStatus.Unsafe)]
    [InlineData(false, false, false, true, SessionHistoryStatus.RecordingConfirmed, false, TranscriptDeleteStatus.NotFinished)]
    [InlineData(false, false, false, true, SessionHistoryStatus.Finished, false, TranscriptDeleteStatus.Deleted)]
    [InlineData(false, false, false, true, SessionHistoryStatus.Finished, true, TranscriptDeleteStatus.Failed)]
    public void DeletionRules_RefuseReparsePointsDirectoriesAndUnfinishedTranscripts(
        bool missing,
        bool reparse,
        bool directory,
        bool regular,
        SessionHistoryStatus? footer,
        bool readFailed,
        TranscriptDeleteStatus expected)
        => Assert.Equal(expected, TranscriptDeletionRules.Classify(missing, reparse, directory, regular, footer, readFailed));

    [Fact]
    public void Filenames_DoNotCollideWhenTheIdentifierDiffers()
    {
        var stamp = new DateTimeOffset(2026, 10, 19, 4, 0, 0, TimeSpan.Zero);
        var first = TranscriptFilenameGenerator.BuildFileName("HOWE", "Session", true, stamp, "AAAA");
        var second = TranscriptFilenameGenerator.BuildFileName("HOWE", "Session", true, stamp, "BBBB");
        Assert.NotEqual(first, second);
        Assert.StartsWith("2026-10-19_04-00-00_HOWE_Session_", first, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckpointVersion_IsConsistentAcrossAboutAndThePackageMapper()
    {
        Assert.Equal("0.1.0-beta.2", ProductVersion.Current);
        Assert.Equal("0.1.0~beta.2-1", DebianPackageVersion.FromInformational(ProductVersion.Current));
    }

    [Fact]
    public void WindowsCopy_IsPresentInEnglishAndPolish()
    {
        var english = File.ReadAllText(Path.Combine(LocalizationDirectory(), "en-US.json"));
        var polish = File.ReadAllText(Path.Combine(LocalizationDirectory(), "pl-PL.json"));
        Assert.Contains("The User Account Control prompt was canceled.", english, StringComparison.Ordinal);
        Assert.Contains("Pytanie Kontroli konta użytkownika zostało anulowane.", polish, StringComparison.Ordinal);
        Assert.Contains("żądano", polish, StringComparison.Ordinal);
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

    private sealed class FakeWindowsLauncher : IWindowsSessionLauncher
    {
        public WindowsSessionStart Result { get; init; }

        public WindowsLaunchPlan? Plan { get; private set; }

        public string Marker => Plan?.Elevate == true && !Plan.QuotedArguments.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase)
            ? "runas-not-shell"
            : "";

        public WindowsSessionStart Start(WindowsLaunchPlan plan)
        {
            Plan = plan;
            return Result;
        }
    }

    private sealed class CountingWatcher : ITranscriptWatcher
    {
        public int Calls { get; private set; }

        public Task<bool> WaitForHeaderAsync(string transcriptPath, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(false);
        }
    }

    private sealed class CancelledRecordingService : ISessionRecordingService
    {
        public SessionRuntimeCapabilities Capabilities { get; } = new(true, true, true);

        public void RefreshDependencies()
        {
        }

        public Task<SessionRecordingResult> StartAsync(
            SessionConfiguration configuration,
            string? language,
            CancellationToken cancellationToken)
            => Task.FromResult(new SessionRecordingResult(SessionRecordingStatus.ElevationCancelled, null));
    }
}
