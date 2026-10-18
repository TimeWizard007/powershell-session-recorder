using System.Globalization;
using System.Text;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Builds Windows-safe transcript filenames and allocates a name that does not
/// collide with a file the caller reports as already present.
/// </summary>
public static class TranscriptFilenameGenerator
{
    public const string Extension = ".txt";

    public const int MaxSegmentLength = 60;

    public const int MaxStemLength = 240;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM\u00B9", "COM\u00B2", "COM\u00B3",
        "LPT\u00B9", "LPT\u00B2", "LPT\u00B3"
    };

    public static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(IsAllowed(ch) ? ch : '_');
        }

        var sanitized = TrimDecorations(CollapseUnderscores(builder.ToString()));
        if (sanitized.Length == 0)
            return "";

        if (IsReservedDeviceName(sanitized))
            sanitized = "_" + sanitized;

        if (sanitized.Length > MaxSegmentLength)
            sanitized = TrimDecorations(sanitized[..MaxSegmentLength]);

        if (sanitized.Length == 0)
            return "";

        if (IsReservedDeviceName(sanitized))
            sanitized = "_" + sanitized;

        return sanitized;
    }

    public static string BuildFileName(SessionConfiguration configuration, DateTimeOffset timestamp, string uniqueId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return BuildFileName(
            configuration.CustomerTag,
            configuration.SessionName,
            configuration.IncludeDateAndTime,
            timestamp,
            uniqueId);
    }

    public static string BuildFileName(
        string? customerTag,
        string? sessionName,
        bool includeDateAndTime,
        DateTimeOffset timestamp,
        string uniqueId)
    {
        var tag = RequireSegment(SanitizeSegment(customerTag), nameof(customerTag));
        var name = RequireSegment(SanitizeSegment(sessionName), nameof(sessionName));
        var id = RequireSegment(SanitizeSegment(uniqueId), nameof(uniqueId));

        var stamp = timestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var stem = includeDateAndTime
            ? $"{stamp}_{tag}_{name}_{id}"
            : $"{tag}_{name}_{id}";

        stem = FitStem(stem);
        if (stem.Length == 0)
            throw new InvalidOperationException("The transcript filename could not be made Windows-safe.");

        return stem + Extension;
    }

    /// <summary>
    /// Returns a filename that <paramref name="fileExists"/> reports as free.
    /// The existence check is applied to the full path. Callers must perform
    /// this check again when a session is actually created.
    /// </summary>
    public static string CreateUniqueFileName(
        string directory,
        string? customerTag,
        string? sessionName,
        bool includeDateAndTime,
        DateTimeOffset timestamp,
        Func<string> uniqueIdFactory,
        Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(uniqueIdFactory);
        ArgumentNullException.ThrowIfNull(fileExists);

        const int maxAttempts = 32;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var fileName = BuildFileName(customerTag, sessionName, includeDateAndTime, timestamp, uniqueIdFactory());
            var fullPath = Path.Combine(directory, fileName);
            if (!fileExists(fullPath))
                return fileName;
        }

        throw new InvalidOperationException("A unique transcript filename could not be allocated.");
    }

    private static string RequireSegment(string segment, string paramName)
    {
        if (segment.Length == 0)
            throw new ArgumentException("The value does not contain a usable filename segment.", paramName);

        return segment;
    }

    private static bool IsAllowed(char value)
    {
        if (value < 32 || value == 127)
            return false;

        return value is not ('<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')
            && !char.IsWhiteSpace(value);
    }

    private static string CollapseUnderscores(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasUnderscore = false;
        foreach (var ch in value)
        {
            if (ch == '_')
            {
                if (previousWasUnderscore)
                    continue;

                previousWasUnderscore = true;
            }
            else
            {
                previousWasUnderscore = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string FitStem(string value)
    {
        var stem = TrimDecorations(value);
        if (stem.Length > MaxStemLength)
            stem = TrimDecorations(stem[..MaxStemLength]);

        if (IsReservedDeviceName(stem))
            stem = "_" + stem;

        if (stem.Length > MaxStemLength)
            stem = TrimDecorations(stem[..MaxStemLength]);

        return IsReservedDeviceName(stem) ? "" : stem;
    }

    private static string TrimDecorations(string value)
        => value.Trim(' ', '.', '_');

    private static bool IsReservedDeviceName(string value)
    {
        if (value.Length == 0)
            return false;

        var separator = value.IndexOf('.');
        var baseName = separator >= 0 ? value[..separator] : value;
        return ReservedDeviceNames.Contains(baseName);
    }
}
