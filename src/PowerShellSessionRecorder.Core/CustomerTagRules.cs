namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Customer TAGs use the same filename rules as a session configuration.
/// Comparison for duplicates is the trimmed TAG, case-insensitive.
/// </summary>
public static class CustomerTagRules
{
    public const int MaxDisplayNameLength = 200;

    public static bool IsValid(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        return TranscriptFilenameGenerator.SanitizeSegment(tag).Length > 0;
    }

    public static string NormalizeTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var trimmed = tag.Trim();
        if (!IsValid(trimmed))
            throw new ArgumentException("The customer TAG cannot be used for a transcript file name.", nameof(tag));

        return trimmed;
    }

    public static bool IsDisplayNameValid(string? displayName)
    {
        if (displayName is null)
            return false;

        var trimmed = displayName.Trim();
        return trimmed.Length <= MaxDisplayNameLength && trimmed.All(character => !char.IsControl(character));
    }

    public static string NormalizeDisplayName(string? displayName)
    {
        var trimmed = (displayName ?? "").Trim();
        if (!IsDisplayNameValid(trimmed))
            throw new ArgumentException("The display name is not valid.", nameof(displayName));

        return trimmed;
    }

    public static bool TagsConflict(string left, string right)
        => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
