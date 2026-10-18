using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services.Recording;

public enum SessionRecordingStatus
{
    Started,
    Unconfirmed,
    Rejected,
    DirectoryFailed,
    AllocationFailed,
    LaunchFailed
}

public sealed record SessionRuntimeCapabilities(
    bool PowerShell7Available,
    bool TerminalAvailable,
    bool PlatformSupported = true);

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

    public SessionRecordingService(
        IExecutableProbe probe,
        IProcessRunner processRunner,
        ITranscriptWatcher transcriptWatcher,
        BootstrapFileStore bootstrapFiles,
        TranscriptFileAllocator? allocator = null,
        Func<DateTimeOffset>? clock = null,
        Func<string>? uniqueIdFactory = null,
        Func<string?>? workingDirectory = null)
    {
        _probe = probe;
        _processRunner = processRunner;
        _transcriptWatcher = transcriptWatcher;
        _bootstrapFiles = bootstrapFiles;
        _allocator = allocator ?? new TranscriptFileAllocator();
        _clock = clock ?? (() => DateTimeOffset.Now);
        _uniqueIdFactory = uniqueIdFactory ?? (() => Guid.NewGuid().ToString("N")[..16].ToUpperInvariant());
        _workingDirectory = workingDirectory ?? DefaultWorkingDirectory;
    }

    public SessionRuntimeCapabilities Capabilities => new(
        _probe.FindExecutable(RecordingPlatform.PowerShellExecutable) is not null,
        _probe.FindExecutable(GnomeTerminalCommand.ExecutableName) is not null,
        RecordingPlatform.IsCurrentPlatformSupported());

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

    private bool CanAttempt(SessionConfiguration configuration)
    {
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
