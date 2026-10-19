namespace PowerShellSessionRecorder.Core;

public interface ISessionHistoryStore
{
    HistoryCatalog Load();

    void Append(SessionHistoryEntry entry);

    void RecordCompletion(string id, DateTimeOffset endedUtc);
}
