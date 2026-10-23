using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services.Recording;

public enum SessionRecordingStatus
{
    Started,
    Unconfirmed,
    Rejected,
    DirectoryFailed,
    AllocationFailed,
    LaunchFailed,
    ElevationCancelled
}

public sealed record SessionRuntimeCapabilities(
    bool PowerShell7Available,
    bool TerminalAvailable,
    bool PlatformSupported = true,
    bool WindowsPowerShellAvailable = false,
    bool ElevationAvailable = false);

public sealed record SessionRecordingResult(SessionRecordingStatus Status, string? TranscriptPath);

public interface ISessionRecordingService
{
    SessionRuntimeCapabilities Capabilities { get; }

    void RefreshDependencies();

    Task<SessionRecordingResult> StartAsync(
        SessionConfiguration configuration,
        string? language,
        CancellationToken cancellationToken);
}

public sealed class SessionRecordingService : ISessionRecordingService
{
    private readonly IExecutableProbe _probe;
    private readonly IProcessRunner _processRunner;
    private readonly ITranscriptWatcher _transcriptWatcher;
    private readonly BootstrapFileStore _bootstrapFiles;
    private readonly TranscriptFileAllocator _allocator;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<string> _uniqueIdFactory;
    private readonly Func<string?> _workingDirectory;
    private readonly IWindowsSessionLauncher _windowsLauncher;

    public SessionRecordingService(
        IExecutableProbe probe,
        IProcessRunner processRunner,
        ITranscriptWatcher transcriptWatcher,
        BootstrapFileStore bootstrapFiles,
        TranscriptFileAllocator? allocator = null,
        Func<DateTimeOffset>? clock = null,
        Func<string>? uniqueIdFactory = null,
        Func<string?>? workingDirectory = null,
        IWindowsSessionLauncher? windowsLauncher = null)
    {
        _probe = probe;
        _processRunner = processRunner;
        _transcriptWatcher = transcriptWatcher;
        _bootstrapFiles = bootstrapFiles;
        _allocator = allocator ?? new TranscriptFileAllocator();
        _clock = clock ?? (() => DateTimeOffset.Now);
        _uniqueIdFactory = uniqueIdFactory ?? (() => Guid.NewGuid().ToString("N")[..16].ToUpperInvariant());
        _workingDirectory = workingDirectory ?? DefaultWorkingDirectory;
        _windowsLauncher = windowsLauncher ?? new WindowsConsoleLauncher();
    }

    public SessionRuntimeCapabilities Capabilities
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var engines = DiscoverWindowsEngines();
                return new SessionRuntimeCapabilities(
                    engines.Any(engine => engine.Version == PowerShellVersion.PowerShell7),
                    true,
                    true,
                    engines.Any(engine => engine.Version == PowerShellVersion.WindowsPowerShell51),
                    true);
            }

            return new SessionRuntimeCapabilities(
                _probe.FindExecutable(RecordingPlatform.PowerShellExecutable) is not null,
                _probe.FindExecutable(GnomeTerminalCommand.ExecutableName) is not null,
                RecordingPlatform.IsCurrentPlatformSupported());
        }
    }

    public void RefreshDependencies() => _probe.Invalidate();

    public async Task<SessionRecordingResult> StartAsync(
        SessionConfiguration configuration,
        string? language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!CanAttempt(configuration))
            return new SessionRecordingResult(SessionRecordingStatus.Rejected, null);

        string transcriptPath;
        try
        {
            transcriptPath = _allocator.Allocate(
                configuration.LogDirectory,
                configuration.CustomerTag,
                configuration.SessionName,
                configuration.IncludeDateAndTime,
                _clock(),
                _uniqueIdFactory);
        }
        catch (UnauthorizedAccessException)
        {
            return new SessionRecordingResult(SessionRecordingStatus.DirectoryFailed, null);
        }
        catch (IOException)
        {
            return new SessionRecordingResult(SessionRecordingStatus.DirectoryFailed, null);
        }
        catch (InvalidOperationException)
        {
            return new SessionRecordingResult(SessionRecordingStatus.AllocationFailed, null);
        }

        try
        {
            if (OperatingSystem.IsWindows())
                return await StartWindowsAsync(configuration, language, transcriptPath, cancellationToken);

            var powerShell = _probe.FindExecutable(RecordingPlatform.PowerShellExecutable);
            var terminal = _probe.FindExecutable(GnomeTerminalCommand.ExecutableName);
            if (powerShell is null || terminal is null)
            {
                TranscriptFileAllocator.DeleteIfStillEmpty(transcriptPath);
                return new SessionRecordingResult(SessionRecordingStatus.Rejected, null);
            }

            var workingDirectory = _workingDirectory() ?? Path.GetDirectoryName(transcriptPath) ?? configuration.LogDirectory;
            var bootstrapPath = _bootstrapFiles.WriteScript();
            var powerShellArguments = RecordingBootstrap.BuildPowerShellArguments(
                workingDirectory,
                bootstrapPath,
                transcriptPath,
                language);
            var command = new List<string>(powerShellArguments.Count + 1) { powerShell };
            command.AddRange(powerShellArguments);
            var terminalArguments = GnomeTerminalCommand.BuildArguments(BuildTitle(configuration.CustomerTag), command);

            var exitCode = await _processRunner.RunAsync(terminal, terminalArguments, cancellationToken);
            if (exitCode != 0)
            {
                TranscriptFileAllocator.DeleteIfStillEmpty(transcriptPath);
                return new SessionRecordingResult(SessionRecordingStatus.LaunchFailed, null);
            }

            var confirmed = await _transcriptWatcher.WaitForHeaderAsync(
                transcriptPath,
                TimeSpan.FromSeconds(10),
                cancellationToken);
            if (confirmed)
                TranscriptFileAllocator.RestrictToCurrentUser(transcriptPath);
            return new SessionRecordingResult(
                confirmed ? SessionRecordingStatus.Started : SessionRecordingStatus.Unconfirmed,
                transcriptPath);
        }
        catch (Exception)
        {
            TranscriptFileAllocator.DeleteIfStillEmpty(transcriptPath);
            return new SessionRecordingResult(SessionRecordingStatus.LaunchFailed, null);
        }
    }

    private async Task<SessionRecordingResult> StartWindowsAsync(
        SessionConfiguration configuration,
        string? language,
        string transcriptPath,
        CancellationToken cancellationToken)
    {
        var engine = DiscoverWindowsEngines().FirstOrDefault(item => item.Version == configuration.PowerShellVersion);
        if (engine is null)
        {
            TranscriptFileAllocator.DeleteIfStillEmpty(transcriptPath);
            return new SessionRecordingResult(SessionRecordingStatus.Rejected, null);
        }

        var bootstrapPath = _bootstrapFiles.WriteScript();
        var inspection = WindowsPrivateMetadata.Inspect(engine.Path);
        var starter = new WindowsSessionStarter(_windowsLauncher, _transcriptWatcher);
        return await starter.StartAsync(
            configuration,
            language,
            engine.Path,
            inspection.ReparsePoint,
            bootstrapPath,
            transcriptPath,
            _workingDirectory(),
            cancellationToken);
    }

    private static IReadOnlyList<PowerShellEngineLocation> DiscoverWindowsEngines()
        => PowerShellEngineDiscovery.DiscoverWindows(
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetEnvironmentVariable("SystemRoot") ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.CurrentDirectory,
            WindowsPrivateMetadata.Inspect);

    private bool CanAttempt(SessionConfiguration configuration)
    {
        if (OperatingSystem.IsWindows())
            return CanAttemptWindows(configuration);

        if (configuration.RunAsAdministrator)
            return false;

        if (configuration.PowerShellVersion != PowerShellVersion.PowerShell7)
            return false;

        if (string.IsNullOrWhiteSpace(configuration.LogDirectory))
            return false;

        if (File.Exists(configuration.LogDirectory) && !Directory.Exists(configuration.LogDirectory))
            return false;

        if (!SessionConfigurationValidator.Validate(configuration).IsValid)
            return false;

        return Capabilities is { PlatformSupported: true, PowerShell7Available: true, TerminalAvailable: true };
    }

    private bool CanAttemptWindows(SessionConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.LogDirectory))
            return false;

        if (File.Exists(configuration.LogDirectory) && !Directory.Exists(configuration.LogDirectory))
            return false;

        if (!SessionConfigurationValidator.Validate(configuration).IsValid)
            return false;

        return DiscoverWindowsEngines().Any(engine => engine.Version == configuration.PowerShellVersion);
    }

    private static string BuildTitle(string? customerTag)
    {
        var tag = TranscriptFilenameGenerator.SanitizeSegment(customerTag);
        return tag.Length == 0
            ? "PowerShell Session Recorder"
            : "PowerShell Session Recorder — " + tag;
    }

    private static string? DefaultWorkingDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Directory.Exists(home) ? home : null;
    }
}
