namespace PowerShellSessionRecorder.Core;

public readonly record struct TranscriptObservation(SessionHistoryStatus Status, DateTimeOffset? EndedUtc);

public interface ITranscriptStatusProbe
{
    TranscriptObservation Observe(string? transcriptPath);
}
