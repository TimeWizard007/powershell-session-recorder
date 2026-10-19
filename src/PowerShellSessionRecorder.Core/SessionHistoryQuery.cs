namespace PowerShellSessionRecorder.Core;

public static class SessionHistoryQuery
{
    public static IReadOnlyList<SessionHistoryEntry> Apply(
        IEnumerable<SessionHistoryEntry> entries,
        string? search,
        string? customerTag,
        DateTime? fromLocalDate,
        DateTime? toLocalDate)
    {
        var term = search?.Trim();
        var tag = customerTag?.Trim();

        return entries
            .Where(entry => MatchesSearch(entry, term))
            .Where(entry => MatchesTag(entry, tag))
            .Where(entry => MatchesDate(entry, fromLocalDate, toLocalDate))
            .OrderByDescending(entry => entry.StartedUtc)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static bool MatchesSearch(SessionHistoryEntry entry, string? term)
    {
        if (string.IsNullOrEmpty(term))
            return true;

        return entry.CustomerTag.Contains(term, StringComparison.OrdinalIgnoreCase)
            || entry.SessionName.Contains(term, StringComparison.OrdinalIgnoreCase)
            || entry.TranscriptFileName.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesTag(SessionHistoryEntry entry, string? tag)
    {
        if (string.IsNullOrEmpty(tag))
            return true;

        return string.Equals(entry.CustomerTag, tag, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDate(SessionHistoryEntry entry, DateTime? fromLocalDate, DateTime? toLocalDate)
    {
        var localDate = entry.StartedUtc.ToLocalTime().Date;
        if (fromLocalDate is not null && localDate < fromLocalDate.Value.Date)
            return false;

        if (toLocalDate is not null && localDate > toLocalDate.Value.Date)
            return false;

        return true;
    }
}
