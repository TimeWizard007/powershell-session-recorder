using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class SessionRecordingServiceTests
{
    private const string MaliciousTag = "'; Remove-Item -LiteralPath * -Recurse -Force";
    private const string MaliciousName = "$(Invoke-Expression 'whoami')";

    [Fact]
    public async Task StartAsync_DoesNotCreateAFileWhenPowerShellIsMissing()
    {
        using var root = new TempDirectory();
        var probe = ReadyProbe();
        probe.PowerShell = null;
        var runner = new FakeRunner();
        var service = CreateService(root.Path, probe, runner, new FakeWatcher());

        var result = await service.StartAsync(Valid(root.Path), "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.Rejected, result.Status);
        Assert.Empty(Directory.EnumerateFiles(root.Path));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task StartAsync_DoesNotCreateAFileWhenTheTerminalIsMissing()
    {
        using var root = new TempDirectory();
        var probe = ReadyProbe();
        probe.Terminal = null;
        var runner = new FakeRunner();
        var service = CreateService(root.Path, probe, runner, new FakeWatcher());

        var result = await service.StartAsync(Valid(root.Path), "en-US", CancellationToken.None);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(SessionRecordingStatus.Unconfirmed, result.Status);
            Assert.NotNull(result.TranscriptPath);
            Assert.True(File.Exists(result.TranscriptPath));
            Assert.EndsWith("pwsh.exe", runner.FileNames.Single(), StringComparison.OrdinalIgnoreCase);
            return;
        }

        Assert.Equal(SessionRecordingStatus.Rejected, result.Status);
        Assert.Empty(Directory.EnumerateFiles(root.Path));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task StartAsync_DeletesTheReservedFileWhenTheTerminalFails()
    {
        using var root = new TempDirectory();
        var runner = new FakeRunner { ExitCode = 1 };
        var service = CreateService(root.Path, ReadyProbe(), runner, new FakeWatcher());

        var result = await service.StartAsync(Valid(root.Path), "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.LaunchFailed, result.Status);
        Assert.Null(result.TranscriptPath);
        Assert.Empty(Directory.EnumerateFiles(root.Path, "*.txt"));
    }

    [Fact]
    public async Task StartAsync_ConfirmsRecordingOnlyAfterTheHeaderIsObserved()
    {
        using var root = new TempDirectory();
        var watcher = new FakeWatcher { Confirmed = true };
        var service = CreateService(root.Path, ReadyProbe(), new FakeRunner(), watcher);

        var result = await service.StartAsync(Valid(root.Path), "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.Started, result.Status);
        Assert.True(File.Exists(result.TranscriptPath));
    }

    [Fact]
    public async Task StartAsync_DoesNotClaimSuccessWhenTheHeaderIsMissing()
    {
        using var root = new TempDirectory();
        var watcher = new FakeWatcher { Confirmed = false };
        var service = CreateService(root.Path, ReadyProbe(), new FakeRunner(), watcher);

        var result = await service.StartAsync(Valid(root.Path), "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.Unconfirmed, result.Status);
        Assert.NotEqual(SessionRecordingStatus.Started, result.Status);
        Assert.True(File.Exists(result.TranscriptPath));
    }

    [Fact]
    public async Task StartAsync_StartsIndependentSessions()
    {
        using var root = new TempDirectory();
        var service = CreateService(root.Path, ReadyProbe(), new FakeRunner(), new FakeWatcher { Confirmed = true });

        var first = await service.StartAsync(Valid(root.Path, "HOWE", "One"), "en-US", CancellationToken.None);
        var second = await service.StartAsync(Valid(root.Path, "OTHER", "Two"), "pl-PL", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.Started, first.Status);
        Assert.Equal(SessionRecordingStatus.Started, second.Status);
        Assert.NotEqual(first.TranscriptPath, second.TranscriptPath);
        Assert.True(File.Exists(first.TranscriptPath));
        Assert.True(File.Exists(second.TranscriptPath));
    }

    [Fact]
    public async Task StartAsync_KeepsMaliciousNamesOutOfTheBootstrapScript()
    {
        using var root = new TempDirectory();
        var runner = new FakeRunner();
        var service = CreateService(root.Path, ReadyProbe(), runner, new FakeWatcher { Confirmed = true });
        var configuration = Valid(root.Path, MaliciousTag, MaliciousName);

        var result = await service.StartAsync(configuration, "en-US", CancellationToken.None);

        var arguments = runner.Calls.Single();
        var transcriptPath = arguments[arguments.ToList().IndexOf("-TranscriptPath") + 1];
        var bootstrapPath = arguments[arguments.ToList().IndexOf("-File") + 1];
        var script = File.ReadAllText(bootstrapPath);

        Assert.Equal(SessionRecordingStatus.Started, result.Status);
        Assert.Equal(transcriptPath, result.TranscriptPath);
        Assert.Equal(RecordingBootstrap.Script, script);
        Assert.DoesNotContain(MaliciousTag, script, StringComparison.Ordinal);
        Assert.DoesNotContain(MaliciousName, script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", script, StringComparison.Ordinal);
        Assert.DoesNotContain("-e", arguments);
        Assert.DoesNotContain("sh", arguments);
        if (OperatingSystem.IsWindows())
            Assert.EndsWith("pwsh.exe", runner.FileNames.Single(), StringComparison.OrdinalIgnoreCase);
        else
            Assert.Equal("/usr/bin/gnome-terminal", runner.FileNames.Single());
    }

    [Theory]
    [InlineData(true, false, SessionRecordingStatus.Rejected)]
    public async Task StartAsync_RejectsElevationAndInvalidConfiguration(bool administrator, bool validNames, SessionRecordingStatus expected)
    {
        using var root = new TempDirectory();
        var service = CreateService(root.Path, ReadyProbe(), new FakeRunner(), new FakeWatcher());
        var configuration = Valid(root.Path);
        configuration.RunAsAdministrator = administrator;
        if (!validNames)
            configuration.CustomerTag = "";

        var result = await service.StartAsync(configuration, "en-US", CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Empty(Directory.EnumerateFiles(root.Path, "*.txt"));
    }

    [Fact]
    public async Task StartAsync_RejectsAnInvalidConfiguration()
    {
        using var root = new TempDirectory();
        var service = CreateService(root.Path, ReadyProbe(), new FakeRunner(), new FakeWatcher());
        var configuration = Valid(root.Path);
        configuration.SessionName = "   ";

        var result = await service.StartAsync(configuration, "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.Rejected, result.Status);
        Assert.Empty(Directory.EnumerateFiles(root.Path, "*.txt"));
    }

    [Fact]
    public async Task StartAsync_ReportsADirectoryThatCannotBeCreated()
    {
        using var root = new TempDirectory();
        var blocker = Path.Combine(root.Path, "blocked");
        File.WriteAllText(blocker, "keep");
        var service = CreateService(root.Path, ReadyProbe(), new FakeRunner(), new FakeWatcher());

        var result = await service.StartAsync(Valid(blocker + "/logs"), "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.DirectoryFailed, result.Status);
        Assert.Equal("keep", File.ReadAllText(blocker));
    }

    [Fact]
    public async Task StartAsync_ReportsAllocationFailureWithoutOverwriting()
    {
        using var root = new TempDirectory();
        var stamp = new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);
        var existing = Path.Combine(root.Path, TranscriptFilenameGenerator.BuildFileName("HOWE", "Session", false, stamp, "SAMEID"));
        File.WriteAllText(existing, "keep");
        var service = CreateService(root.Path, ReadyProbe(), new FakeRunner(), new FakeWatcher(), () => stamp, () => "SAMEID");

        var result = await service.StartAsync(Valid(root.Path), "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.AllocationFailed, result.Status);
        Assert.Equal("keep", File.ReadAllText(existing));
    }

    [Fact]
    public async Task StartAsync_ReportsLaunchFailureWhenTheProcessCannotStart()
    {
        using var root = new TempDirectory();
        var runner = new FakeRunner { Exception = new IOException("terminal missing") };
        var service = CreateService(root.Path, ReadyProbe(), runner, new FakeWatcher());

        var result = await service.StartAsync(Valid(root.Path), "en-US", CancellationToken.None);

        Assert.Equal(SessionRecordingStatus.LaunchFailed, result.Status);
        Assert.Empty(Directory.EnumerateFiles(root.Path, "*.txt"));
    }

    [Fact]
    public void ExecutableLocator_FindsAnExecutableAndIgnoresAMissingOne()
    {
        using var root = new TempDirectory();
        var executable = Path.Combine(root.Path, "pwsh");
        File.WriteAllText(executable, "");

        Assert.Equal(executable, ExecutableLocator.Find("pwsh", root.Path, path => path == executable));
        Assert.Null(ExecutableLocator.Find("gnome-terminal", root.Path, path => path == executable));
        Assert.Null(ExecutableLocator.Find("pwsh", "", _ => true));
    }

    private static SessionRecordingService CreateService(
        string logDirectory,
        FakeProbe probe,
        FakeRunner runner,
        FakeWatcher watcher,
        Func<DateTimeOffset>? clock = null,
        Func<string>? uniqueId = null)
        => new(
            probe,
            runner,
            watcher,
            new BootstrapFileStore(Path.Combine(logDirectory, "bootstrap")),
            clock: clock,
            uniqueIdFactory: uniqueId,
            workingDirectory: () => logDirectory,
            windowsLauncher: new RecordingWindowsLauncher(runner),
            windowsEngines: () => WindowsEngines(probe, logDirectory));

    private static FakeProbe ReadyProbe() => new();

    private static IReadOnlyList<PowerShellEngineLocation> WindowsEngines(FakeProbe probe, string logDirectory)
    {
        if (!OperatingSystem.IsWindows() || probe.PowerShell is null)
            return [];

        var engine = Path.GetFullPath(Path.Combine(logDirectory, "pwsh.exe"));
        if (!File.Exists(engine))
            File.WriteAllBytes(engine, []);

        return [new PowerShellEngineLocation(PowerShellVersion.PowerShell7, engine)];
    }

    private static SessionConfiguration Valid(string directory, string tag = "HOWE", string name = "Session")
        => new()
        {
            CustomerTag = tag,
            SessionName = name,
            LogDirectory = directory,
            PowerShellVersion = PowerShellVersion.PowerShell7,
            IncludeDateAndTime = false
        };

    private sealed class FakeProbe : IExecutableProbe
    {
        public string? PowerShell { get; set; } = "/usr/bin/pwsh";

        public string? Terminal { get; set; } = "/usr/bin/gnome-terminal";

        public string? FindExecutable(string name)
            => name switch
            {
                "pwsh" => PowerShell,
                GnomeTerminalCommand.ExecutableName => Terminal,
                _ => null
            };

        public void Invalidate()
        {
        }
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public int ExitCode { get; set; }

        public Exception? Exception { get; set; }

        public List<string> FileNames { get; } = [];

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            FileNames.Add(fileName);
            Calls.Add(arguments);
            if (Exception is not null)
                throw Exception;

            return Task.FromResult(ExitCode);
        }
    }

    private sealed class RecordingWindowsLauncher : IWindowsSessionLauncher
    {
        private readonly FakeRunner _runner;

        public RecordingWindowsLauncher(FakeRunner runner) => _runner = runner;

        public WindowsSessionStart Start(WindowsLaunchPlan plan)
        {
            _runner.FileNames.Add(plan.ExecutablePath);
            _runner.Calls.Add(plan.Arguments);
            if (_runner.Exception is not null)
                throw _runner.Exception;

            return _runner.ExitCode == 0 ? WindowsSessionStart.Started : WindowsSessionStart.Failed;
        }
    }

    private sealed class FakeWatcher : ITranscriptWatcher
    {
        public bool Confirmed { get; set; }

        public Task<bool> WaitForHeaderAsync(string transcriptPath, TimeSpan timeout, CancellationToken cancellationToken)
            => Task.FromResult(Confirmed);
    }
}
