namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Shared decision for transcript deletion. The caller deletes a file only when the result is Deleted.
/// </summary>
public static class TranscriptDeletionRules
{
    public static TranscriptDeleteStatus Classify(
        bool missing,
        bool reparsePoint,
        bool directory,
        bool regularFile,
        SessionHistoryStatus? footer,
        bool readFailed)
    {
        if (missing)
            return TranscriptDeleteStatus.Missing;

        if (reparsePoint || directory || !regularFile)
            return TranscriptDeleteStatus.Unsafe;

        if (readFailed || footer is null)
            return TranscriptDeleteStatus.Failed;

        if (footer == SessionHistoryStatus.Finished)
            return TranscriptDeleteStatus.Deleted;

        return footer == SessionHistoryStatus.Unknown
            ? TranscriptDeleteStatus.Failed
            : TranscriptDeleteStatus.NotFinished;
    }
}
