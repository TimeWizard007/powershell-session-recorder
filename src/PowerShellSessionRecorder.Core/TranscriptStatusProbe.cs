using System.Globalization;
using System.Text;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Reads only the end of a transcript. A footer is the only completion signal.
/// The file is never executed.
/// </summary>
public sealed class TranscriptStatusProbe : ITranscriptStatusProbe
{
    public const string EndMarker = "PowerShell transcript end";

    public const int TailBytes = 8192;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public TranscriptObservation Observe(string? transcriptPath)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath))
            return new TranscriptObservation(SessionHistoryStatus.Unknown, null);

        string fullPath;
        try
        {
            if (transcriptPath.Any(character => Path.GetInvalidPathChars().Contains(character)))
                return new TranscriptObservation(SessionHistoryStatus.Unknown, null);

            fullPath = Path.GetFullPath(transcriptPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new TranscriptObservation(SessionHistoryStatus.Unknown, null);
        }

        if (!File.Exists(fullPath))
            return new TranscriptObservation(SessionHistoryStatus.TranscriptMissing, null);

        try
        {
            var tail = ReadTail(fullPath);
            var marker = tail.IndexOf(EndMarker, StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return new TranscriptObservation(SessionHistoryStatus.RecordingConfirmed, null);

            return new TranscriptObservation(SessionHistoryStatus.Finished, TryParseEndTime(tail[marker..]));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new TranscriptObservation(SessionHistoryStatus.Unknown, null);
        }
    }

    private static string ReadTail(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length > TailBytes)
            stream.Seek(-TailBytes, SeekOrigin.End);

        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static DateTimeOffset? TryParseEndTime(string tail)
    {
        const string label = "End time:";
        var labelAt = tail.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (labelAt < 0)
            return null;

        var value = tail[(labelAt + label.Length)..].TrimStart();
        var digits = new string(value.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length < 14)
            return null;

        if (!DateTime.TryParseExact(
                digits[..14],
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localClock))
            return null;

        var local = DateTime.SpecifyKind(localClock, DateTimeKind.Local);
        return new DateTimeOffset(local).ToUniversalTime();
    }
}
