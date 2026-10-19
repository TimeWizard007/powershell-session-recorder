namespace PowerShellSessionRecorder.Core;

public sealed class SessionHistoryDocument
{
    public int SchemaVersion { get; set; } = JsonHistoryStore.CurrentSchemaVersion;

    public List<SessionHistoryEntry> Sessions { get; set; } = [];
}
