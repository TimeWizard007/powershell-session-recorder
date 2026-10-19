namespace PowerShellSessionRecorder.Core;

public interface ISessionHistoryStore
{
    HistoryCatalog Load();

    void Append(SessionHistoryEntry entry);

    void RecordCompletion(string id, DateTimeOffset endedUtc);

    /// <summary>
    /// Removes the named entries in one catalog rewrite. Returns how many were removed.
    /// Does not delete transcript files.
    /// </summary>
    int Remove(IReadOnlyCollection<string> ids);
}
