using System.ComponentModel;
using System.Diagnostics;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services.Recording;

public enum WindowsSessionStart
{
    Started,
    Cancelled,
    Failed
}

public interface IWindowsSessionLauncher
{
    WindowsSessionStart Start(WindowsLaunchPlan plan);
}

public sealed class WindowsConsoleLauncher : IWindowsSessionLauncher
{
    private const int ErrorCancelled = 1223;

    public WindowsSessionStart Start(WindowsLaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var startInfo = new ProcessStartInfo
        {
            FileName = plan.ExecutablePath,
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = plan.Elevate,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        if (plan.Elevate)
        {
            startInfo.Verb = "runas";
            startInfo.Arguments = plan.QuotedArguments;
        }
        else
        {
            foreach (var argument in plan.Arguments)
                startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            return process is null ? WindowsSessionStart.Failed : WindowsSessionStart.Started;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            return WindowsSessionStart.Cancelled;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return WindowsSessionStart.Failed;
        }
    }
}

public sealed class WindowsSessionStarter
{
    private readonly IWindowsSessionLauncher _launcher;
    private readonly ITranscriptWatcher _watcher;

    public WindowsSessionStarter(IWindowsSessionLauncher launcher, ITranscriptWatcher watcher)
    {
        _launcher = launcher;
        _watcher = watcher;
    }

    public async Task<SessionRecordingResult> StartAsync(
        SessionConfiguration configuration,
        string? language,
        string executablePath,
        bool executableIsReparsePoint,
        string bootstrapPath,
        string transcriptPath,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var directory = workingDirectory ?? Path.GetDirectoryName(transcriptPath) ?? configuration.LogDirectory;
        var title = GnomeTerminalCommand.SanitizeTitle(configuration.CustomerTag);
        if (!WindowsLaunchPlanFactory.TryCreate(
                configuration.PowerShellVersion,
                executablePath,
                directory,
                bootstrapPath,
                transcriptPath,
                language,
                title,
                configuration.RunAsAdministrator,
                executableIsReparsePoint,
                out var plan)
            || plan is null)
        {
            TranscriptFileAllocator.DeleteIfStillEmpty(transcriptPath);
            return new SessionRecordingResult(SessionRecordingStatus.Rejected, null);
        }

        var started = _launcher.Start(plan);
        if (started == WindowsSessionStart.Cancelled)
        {
            TranscriptFileAllocator.DeleteIfStillEmpty(transcriptPath);
            return new SessionRecordingResult(SessionRecordingStatus.ElevationCancelled, null);
        }

        if (started != WindowsSessionStart.Started)
        {
            TranscriptFileAllocator.DeleteIfStillEmpty(transcriptPath);
            return new SessionRecordingResult(SessionRecordingStatus.LaunchFailed, null);
        }

        var confirmed = await _watcher.WaitForHeaderAsync(transcriptPath, TimeSpan.FromSeconds(10), cancellationToken);
        if (confirmed)
            TranscriptFileAllocator.RestrictToCurrentUser(transcriptPath);

        return new SessionRecordingResult(
            confirmed ? SessionRecordingStatus.Started : SessionRecordingStatus.Unconfirmed,
            transcriptPath);
    }
}
