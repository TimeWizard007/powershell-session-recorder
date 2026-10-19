namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Metadata for one confirmed recording. The transcript body is not stored here.
/// </summary>
public sealed class SessionHistoryEntry
{
    public string Id { get; set; } = "";

    public string CustomerTag { get; set; } = "";

    public string SessionName { get; set; } = "";

    public string TranscriptFileName { get; set; } = "";

    public string TranscriptPath { get; set; } = "";

    public DateTimeOffset StartedUtc { get; set; }

    public PowerShellVersion PowerShellVersion { get; set; }

    /// <summary>
    /// Verified recording state. Only <see cref="SessionHistoryStatus.RecordingConfirmed"/>
    /// and <see cref="SessionHistoryStatus.Finished"/> are written.
    /// </summary>
    public SessionHistoryStatus RecordingStatus { get; set; }

    public DateTimeOffset? EndedUtc { get; set; }

    public static SessionHistoryEntry CreateConfirmed(
        SessionConfiguration configuration,
        string transcriptPath,
        DateTimeOffset startedUtc)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptPath);

        var fullPath = Path.GetFullPath(transcriptPath);
        var entry = new SessionHistoryEntry
        {
            Id = Guid.NewGuid().ToString("D"),
            CustomerTag = configuration.CustomerTag.Trim(),
            SessionName = configuration.SessionName.Trim(),
            TranscriptFileName = Path.GetFileName(fullPath),
            TranscriptPath = fullPath,
            StartedUtc = startedUtc.ToUniversalTime(),
            PowerShellVersion = configuration.PowerShellVersion,
            RecordingStatus = SessionHistoryStatus.RecordingConfirmed
        };

        if (!IsPersistable(entry))
            throw new ArgumentException("The confirmed session cannot be stored in history.", nameof(configuration));

        return entry;
    }

    public static bool IsPersistable(SessionHistoryEntry entry)
    {
        if (!Guid.TryParse(entry.Id, out _))
            return false;

        if (string.IsNullOrWhiteSpace(entry.CustomerTag) || string.IsNullOrWhiteSpace(entry.SessionName))
            return false;

        if (string.IsNullOrWhiteSpace(entry.TranscriptFileName) || string.IsNullOrWhiteSpace(entry.TranscriptPath))
            return false;

        if (!Path.IsPathRooted(entry.TranscriptPath))
            return false;

        if (entry.StartedUtc == default)
            return false;

        if (entry.RecordingStatus is not (SessionHistoryStatus.RecordingConfirmed or SessionHistoryStatus.Finished))
            return false;

        return Enum.IsDefined(entry.PowerShellVersion);
    }
}
