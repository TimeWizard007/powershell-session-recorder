namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Recording state that can be shown for a history entry.
/// A missing end time is not treated as an active session.
/// </summary>
public enum SessionHistoryStatus
{
    RecordingConfirmed = 0,
    Finished = 1,
    Unknown = 2,
    TranscriptMissing = 3
}
