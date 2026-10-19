namespace PowerShellSessionRecorder.Core;

public enum HistoryLoadKind
{
    Missing,
    Loaded,
    Quarantined,
    Unreadable,
    UnsupportedSchema
}

public sealed class HistoryCatalog
{
    private HistoryCatalog(HistoryLoadKind kind, IReadOnlyList<SessionHistoryEntry> sessions)
    {
        Kind = kind;
        Sessions = sessions;
    }

    public HistoryLoadKind Kind { get; }

    public IReadOnlyList<SessionHistoryEntry> Sessions { get; }

    public static HistoryCatalog Missing() => new(HistoryLoadKind.Missing, []);

    public static HistoryCatalog Loaded(IReadOnlyList<SessionHistoryEntry> sessions)
        => new(HistoryLoadKind.Loaded, sessions);

    public static HistoryCatalog Quarantined() => new(HistoryLoadKind.Quarantined, []);

    public static HistoryCatalog Unreadable() => new(HistoryLoadKind.Unreadable, []);

    public static HistoryCatalog UnsupportedSchema() => new(HistoryLoadKind.UnsupportedSchema, []);
}
