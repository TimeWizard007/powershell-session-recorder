namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Keeps application metadata owner-only on Unix.
/// Windows keeps the existing file APIs. Symbolic links are not followed on Unix.
/// </summary>
public static class UnixPrivateMetadata
{
    public static bool IsSymbolicLink(string path)
    {
        if (OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false) is not null;
        }
        catch (Exception exception) when (exception is FileNotFoundException
                     or DirectoryNotFoundException
                     or IOException
                     or UnauthorizedAccessException
                     or ArgumentException
                     or NotSupportedException)
        {
            return false;
        }
    }

    public static void PrepareOwnerDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (IsSymbolicLink(directory))
            throw new IOException("Refusing to use a symlinked configuration directory.");

        Directory.CreateDirectory(directory);
        if (IsSymbolicLink(directory))
            throw new IOException("Refusing to use a symlinked configuration directory.");

        TryRestrictDirectory(directory);
    }

    public static void RejectSymbolicLink(string path)
    {
        if (IsSymbolicLink(path))
            throw new IOException("Refusing to follow a symlink.");
    }

    public static void TryRestrictDirectory(string directory)
    {
        if (OperatingSystem.IsWindows() || IsSymbolicLink(directory))
            return;

        try
        {
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

    public static void TryRestrictRegularFile(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        RejectSymbolicLink(path);
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

    public static string CreateUniqueTemporaryPath(string finalPath)
    {
        var directory = Path.GetDirectoryName(finalPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("The metadata path does not include a directory.");

        return Path.Combine(directory, Path.GetFileName(finalPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
    }

    public static void WriteNewPrivateFile(string path, ReadOnlySpan<byte> contents)
    {
        RejectSymbolicLink(path);
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var stream = new FileStream(path, options))
        {
            stream.Write(contents);
            stream.Flush();
        }

        TryRestrictRegularFile(path);
    }
}
