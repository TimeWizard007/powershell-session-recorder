using System.Runtime.InteropServices;
using System.Text;

namespace PowerShellSessionRecorder.Core;

public enum TranscriptDeleteStatus
{
    Deleted,
    Missing,
    NotFinished,
    Unsafe,
    Failed
}

public readonly record struct TranscriptDeleteOutcome(TranscriptDeleteStatus Status);

/// <summary>
/// Deletes one recorded transcript file. A directory is never deleted.
/// On Linux the file is unlinked only after the opened descriptor is a regular file,
/// its tail contains the transcript footer, and the directory entry still names that inode.
/// </summary>
public static partial class TranscriptFileDeleter
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static TranscriptDeleteOutcome TryDelete(string? recordedPath)
    {
        if (string.IsNullOrWhiteSpace(recordedPath) || !Path.IsPathRooted(recordedPath))
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

        string fullPath;
        try
        {
            if (recordedPath.Any(character => Path.GetInvalidPathChars().Contains(character)))
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

            fullPath = Path.GetFullPath(recordedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(fullPath, recordedPath, comparison))
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

        var name = Path.GetFileName(fullPath);
        var parent = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(parent)
            || string.IsNullOrEmpty(name)
            || name is "." or ".."
            || name.Contains('/')
            || name.Contains('\\'))
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

        if (OperatingSystem.IsLinux())
            return LinuxTranscriptDeletion.Delete(parent, name);

        return DeleteManaged(fullPath);
    }

    private static TranscriptDeleteOutcome DeleteManaged(string fullPath)
    {
        if (UnixPrivateMetadata.IsSymbolicLink(fullPath) || Directory.Exists(fullPath))
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

        if (!File.Exists(fullPath))
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Missing);

        var observation = new TranscriptStatusProbe().Observe(fullPath);
        if (observation.Status != SessionHistoryStatus.Finished)
            return new TranscriptDeleteOutcome(observation.Status == SessionHistoryStatus.Unknown
                ? TranscriptDeleteStatus.Failed
                : TranscriptDeleteStatus.NotFinished);

        try
        {
            File.Delete(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);
        }

        return File.Exists(fullPath)
            ? new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed)
            : new TranscriptDeleteOutcome(TranscriptDeleteStatus.Deleted);
    }
}

internal static partial class LinuxTranscriptDeletion
{
    private const int OReadOnly = 0;
    private const int ONoFollow = 0x20000;
    private const int OCloseOnExec = 0x80000;
    private const int ODirectory = 0x10000;
    private const int SeekEnd = 2;
    private const int AtSymlinkNoFollow = 0x100;
    private const int ErrnoNoEntry = 2;
    private const int ErrnoAccess = 13;
    private const int ErrnoLoop = 40;
    private const int ErrnoIsDirectory = 21;
    private const uint FileTypeMask = 0xF000;
    private const uint RegularFile = 0x8000;

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStat
    {
        [FieldOffset(0)]
        public ulong Device;

        [FieldOffset(8)]
        public ulong Inode;

        [FieldOffset(24)]
        public uint Mode;
    }

    public static TranscriptDeleteOutcome Delete(string parent, string name)
    {
        var directory = Open(parent, OReadOnly | ODirectory | ONoFollow | OCloseOnExec);
        if (directory < 0)
            return MapOpenError(Marshal.GetLastPInvokeError(), missingWhenAbsent: true);

        var file = -1;
        try
        {
            file = OpenAt(directory, name, OReadOnly | ONoFollow | OCloseOnExec);
            if (file < 0)
                return MapOpenError(Marshal.GetLastPInvokeError(), missingWhenAbsent: true);

            if (FStat(file, out var opened) != 0 || !IsRegularFile(opened.Mode))
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

            var tail = ReadTail(file);
            if (tail is null)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);

            if (TranscriptStatusProbe.ObserveTail(tail).Status != SessionHistoryStatus.Finished)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.NotFinished);

            if (FStatAt(directory, name, out var named, AtSymlinkNoFollow) != 0
                || named.Device != opened.Device
                || named.Inode != opened.Inode
                || !IsRegularFile(named.Mode))
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

            if (UnlinkAt(directory, name, 0) != 0)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);

            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Deleted);
        }
        finally
        {
            if (file >= 0)
                _ = Close(file);
            _ = Close(directory);
        }
    }

    private static TranscriptDeleteOutcome MapOpenError(int error, bool missingWhenAbsent)
    {
        if (missingWhenAbsent && error == ErrnoNoEntry)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Missing);
        if (error is ErrnoLoop or ErrnoIsDirectory)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);
        if (error == ErrnoAccess)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);
        return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);
    }

    private static bool IsRegularFile(uint mode) => (mode & FileTypeMask) == RegularFile;

    private static string? ReadTail(int file)
    {
        var size = LSeek(file, 0, SeekEnd);
        if (size < 0)
            return null;

        var start = size > TranscriptStatusProbe.TailBytes ? size - TranscriptStatusProbe.TailBytes : 0;
        var length = (int)(size - start);
        if (length == 0)
            return "";

        var buffer = new byte[length];
        var read = PRead(file, buffer, (nuint)length, start);
        if (read < 0)
            return null;

        return Utf8.GetString(buffer, 0, (int)read);
    }

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenAt(int directory, string name, int flags);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int file);

    [LibraryImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static partial int FStat(int file, out LinuxStat stat);

    [LibraryImport("libc", EntryPoint = "fstatat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int FStatAt(int directory, string name, out LinuxStat stat, int flags);

    [LibraryImport("libc", EntryPoint = "unlinkat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int UnlinkAt(int directory, string name, int flags);

    [LibraryImport("libc", EntryPoint = "lseek", SetLastError = true)]
    private static partial long LSeek(int file, long offset, int whence);

    [LibraryImport("libc", EntryPoint = "pread", SetLastError = true)]
    private static partial nint PRead(int file, byte[] buffer, nuint count, long offset);
}
