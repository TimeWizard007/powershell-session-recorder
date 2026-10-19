namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Removes history entries, and optionally their transcript files.
/// Files are deleted before the catalog rewrite. Only entries whose files were
/// deleted, or were already missing, are removed from the catalog. A failed file
/// deletion leaves that history entry in place. The catalog rewrite is one atomic replace.
/// </summary>
public static class HistoryCleanup
{
    public static HistoryCleanupReport Remove(
        ISessionHistoryStore store,
        IReadOnlyList<SessionHistoryEntry> selected,
        bool deleteFiles)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selected);

        var entries = selected
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (!deleteFiles)
        {
            try
            {
                var removed = store.Remove(entries.Select(entry => entry.Id).ToArray());
                return new HistoryCleanupReport
                {
                    Requested = entries.Count,
                    HistoryRemoved = removed,
                    DeletesFiles = false
                };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return new HistoryCleanupReport
                {
                    Requested = entries.Count,
                    HistoryWriteFailed = true,
                    DeletesFiles = false
                };
            }
        }

        var removable = new List<string>();
        var deleted = 0;
        var missing = 0;
        var preserved = 0;
        var refused = 0;
        foreach (var entry in entries)
        {
            switch (TranscriptFileDeleter.TryDelete(entry.TranscriptPath).Status)
            {
                case TranscriptDeleteStatus.Deleted:
                    deleted++;
                    removable.Add(entry.Id);
                    break;
                case TranscriptDeleteStatus.Missing:
                    missing++;
                    removable.Add(entry.Id);
                    break;
                case TranscriptDeleteStatus.NotFinished:
                    preserved++;
                    break;
                default:
                    refused++;
                    break;
            }
        }

        var historyRemoved = 0;
        var historyWriteFailed = false;
        try
        {
            if (removable.Count > 0)
                historyRemoved = store.Remove(removable);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            historyWriteFailed = true;
        }

        return new HistoryCleanupReport
        {
            Requested = entries.Count,
            HistoryRemoved = historyRemoved,
            FilesDeleted = deleted,
            FilesAlreadyMissing = missing,
            FilesPreserved = preserved,
            FilesRefused = refused,
            HistoryWriteFailed = historyWriteFailed,
            DeletesFiles = true
        };
    }
}

public sealed class HistoryCleanupReport
{
    public int Requested { get; init; }

    public int HistoryRemoved { get; init; }

    public int FilesDeleted { get; init; }

    public int FilesAlreadyMissing { get; init; }

    public int FilesPreserved { get; init; }

    public int FilesRefused { get; init; }

    public bool HistoryWriteFailed { get; init; }

    public bool DeletesFiles { get; init; }
}
