using System.Diagnostics;

namespace PowerShellSessionRecorder.App.Services;

public enum TranscriptOpenResult
{
    Opened,
    Missing,
    AccessDenied,
    InvalidPath,
    Failed
}

public interface ITranscriptLocationOpener
{
    TranscriptOpenResult TryOpenFile(string? path);

    TranscriptOpenResult TryOpenFolder(string? path);
}

/// <summary>
/// Opens a transcript or its folder with a structured process argument.
/// The transcript is never executed as a script.
/// </summary>
public sealed class TranscriptLocationOpener : ITranscriptLocationOpener
{
    public TranscriptOpenResult TryOpenFile(string? path)
    {
        if (!TryGetFullPath(path, out var fullPath))
            return TranscriptOpenResult.InvalidPath;

        if (Directory.Exists(fullPath) || !File.Exists(fullPath))
            return TranscriptOpenResult.Missing;

        var access = TryRead(fullPath);
        if (access != TranscriptOpenResult.Opened)
            return access;

        return Start(fullPath);
    }

    public TranscriptOpenResult TryOpenFolder(string? path)
    {
        if (!TryGetFullPath(path, out var fullPath))
            return TranscriptOpenResult.InvalidPath;

        var folder = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return TranscriptOpenResult.Missing;

        return Start(folder);
    }

    private static TranscriptOpenResult TryRead(string fullPath)
    {
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return TranscriptOpenResult.Opened;
        }
        catch (UnauthorizedAccessException)
        {
            return TranscriptOpenResult.AccessDenied;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return TranscriptOpenResult.Failed;
        }
    }

    private static bool TryGetFullPath(string? path, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (path.Any(character => Path.GetInvalidPathChars().Contains(character)))
                return false;

            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static TranscriptOpenResult Start(string target)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = OperatingSystem.IsWindows()
            };

            if (OperatingSystem.IsWindows())
                startInfo.FileName = target;
            else if (OperatingSystem.IsMacOS())
                startInfo.FileName = "open";
            else
                startInfo.FileName = "xdg-open";

            if (!OperatingSystem.IsWindows())
                startInfo.ArgumentList.Add(target);

            return Process.Start(startInfo) is null
                ? TranscriptOpenResult.Failed
                : TranscriptOpenResult.Opened;
        }
        catch (UnauthorizedAccessException)
        {
            return TranscriptOpenResult.AccessDenied;
        }
        catch (Exception)
        {
            return TranscriptOpenResult.Failed;
        }
    }
}
