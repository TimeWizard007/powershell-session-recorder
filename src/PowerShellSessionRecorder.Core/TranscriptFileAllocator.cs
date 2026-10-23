namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Reserves a transcript path with <see cref="FileMode.CreateNew"/> so two
/// sessions cannot select the same file.
/// </summary>
public sealed class TranscriptFileAllocator
{
    public const int MaxAttempts = 32;

    public string Allocate(
        string directory,
        string? customerTag,
        string? sessionName,
        bool includeDateAndTime,
        DateTimeOffset timestamp,
        Func<string> uniqueIdFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(uniqueIdFactory);

        var fullDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDirectory);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var fileName = TranscriptFilenameGenerator.BuildFileName(
                customerTag,
                sessionName,
                includeDateAndTime,
                timestamp,
                uniqueIdFactory());
            var path = Path.Combine(fullDirectory, fileName);

            try
            {
                using (var stream = OpenNewTranscript(path))
                {
                    stream.Flush(flushToDisk: true);
                }

                RestrictToCurrentUser(path);
                return path;
            }
            catch (IOException)
            {
                // The name was taken between planning and creation. Try another id.
            }
        }

        throw new InvalidOperationException("A unique transcript filename could not be allocated.");
    }

    public static void DeleteIfStillEmpty(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            if (OperatingSystem.IsWindows() && WindowsPrivateMetadata.IsReparsePoint(path))
                return;

            if (new FileInfo(path).Length == 0)
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void RestrictToCurrentUser(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (OperatingSystem.IsWindows())
        {
            if (!WindowsPrivateMetadata.IsReparsePoint(path))
                WindowsPrivateMetadata.TryRestrictFile(path);
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

    private static FileStream OpenNewTranscript(string path)
    {
        if (OperatingSystem.IsWindows())
            return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
    }
}
