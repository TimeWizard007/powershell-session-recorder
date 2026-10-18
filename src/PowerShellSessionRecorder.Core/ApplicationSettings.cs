namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Per-user preferences. This model intentionally has no fields for credentials
/// or customer secrets.
/// </summary>
public sealed class ApplicationSettings
{
    public const int CurrentSchemaVersion = 1;

    public const int MaxRecentCustomerTags = 10;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string Language { get; set; } = "en-US";

    public string LastLogDirectory { get; set; } = "";

    public PowerShellVersion LastPowerShellVersion { get; set; } = PowerShellVersion.PowerShell7;

    public bool IncludeDateAndTime { get; set; } = true;

    public bool SaveSessionInHistory { get; set; } = true;

    public List<string> RecentCustomerTags { get; set; } = [];

    public static string CanonicalLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "en-US";

        var normalized = language.Trim().Replace('_', '-');
        if (normalized.Equals("pl", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("pl-PL", StringComparison.OrdinalIgnoreCase))
            return "pl-PL";

        if (normalized.Equals("en", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            return "en-US";

        return "en-US";
    }

    /// <summary>
    /// Repairs values that can be corrected without discarding the file.
    /// Returns <see langword="true"/> when a stored value was changed.
    /// </summary>
    public bool Normalize()
    {
        var changed = false;

        var language = CanonicalLanguage(Language);
        if (!string.Equals(Language, language, StringComparison.Ordinal))
        {
            Language = language;
            changed = true;
        }

        if (!Enum.IsDefined(LastPowerShellVersion))
        {
            LastPowerShellVersion = PowerShellVersion.PowerShell7;
            changed = true;
        }

        var directory = (LastLogDirectory ?? "").Trim();
        if (!string.Equals(LastLogDirectory, directory, StringComparison.Ordinal))
        {
            LastLogDirectory = directory;
            changed = true;
        }

        var tags = NormalizeTags(RecentCustomerTags);
        if (RecentCustomerTags is null || !tags.SequenceEqual(RecentCustomerTags, StringComparer.Ordinal))
        {
            RecentCustomerTags = tags;
            changed = true;
        }

        if (SchemaVersion < CurrentSchemaVersion)
        {
            SchemaVersion = CurrentSchemaVersion;
            changed = true;
        }

        return changed;
    }

    public void RememberCustomerTag(string? customerTag)
    {
        if (string.IsNullOrWhiteSpace(customerTag))
            return;

        RecentCustomerTags ??= [];
        var trimmed = customerTag.Trim();
        RecentCustomerTags.RemoveAll(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase));
        RecentCustomerTags.Insert(0, trimmed);
        if (RecentCustomerTags.Count > MaxRecentCustomerTags)
            RecentCustomerTags.RemoveRange(MaxRecentCustomerTags, RecentCustomerTags.Count - MaxRecentCustomerTags);
    }

    private static List<string> NormalizeTags(List<string>? tags)
    {
        var normalized = new List<string>(MaxRecentCustomerTags);
        if (tags is null)
            return normalized;

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            var trimmed = tag.Trim();
            if (normalized.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
                continue;

            normalized.Add(trimmed);
            if (normalized.Count == MaxRecentCustomerTags)
                break;
        }

        return normalized;
    }
}
