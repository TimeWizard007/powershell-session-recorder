using System.Runtime.InteropServices;
using System.Text;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Deletes one regular transcript on macOS. The directory entry is opened with O_NOFOLLOW,
/// so a symlink is not followed. Modern libSystem exports fstat and fstatat, and those
/// calls already return the 64-bit inode layout. The fstat$INODE64 aliases are static-link
/// names and are not dlsym exports on macOS 15. A directory is classified and is not deleted.
/// A stat result that is not a regular file refuses deletion.
/// </summary>
internal static partial class MacTranscriptDeletion
{
    internal const int NoFollowFlag = 0x0100;
    internal const int CloseOnExecFlag = 0x01000000;
    internal const int DirectoryFlag = 0x00100000;
    internal const int SymlinkNoFollow = 0x0020;
    internal const int CurrentDirectory = -2;
    internal const int SeekEnd = 2;
    internal const int ErrorNoEntry = 2;
    internal const int ErrorAccess = 13;
    internal const int ErrorLoop = 62;
    internal const uint FileTypeMask = 0xF000;
    internal const uint RegularFile = 0x8000;
    internal const int StatSize = 144;
    internal const int DeviceOffset = 0;
    internal const int ModeOffset = 4;
    internal const int InodeOffset = 8;
    internal const string FileStatEntryPoint = "fstat";
    internal const string FileStatAtEntryPoint = "fstatat";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static TranscriptDeleteOutcome Delete(string parent, string name)
    {
        var directory = OpenAt(CurrentDirectory, parent, DirectoryFlag | NoFollowFlag | CloseOnExecFlag);
        if (directory < 0)
            return MapOpenError(Marshal.GetLastPInvokeError());

        var file = -1;
        try
        {
            file = OpenAt(directory, name, NoFollowFlag | CloseOnExecFlag);
            if (file < 0)
                return MapOpenError(Marshal.GetLastPInvokeError());

            if (FileStat(file, out var opened) != 0 || !IsRegularFile(opened.Mode))
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

            var tail = ReadTail(file);
            if (tail is null)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);

            if (TranscriptStatusProbe.ObserveTail(tail).Status != SessionHistoryStatus.Finished)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.NotFinished);

            if (FileStatAt(directory, name, out var named, SymlinkNoFollow) != 0
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

    private static TranscriptDeleteOutcome MapOpenError(int error)
    {
        if (error == ErrorNoEntry)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Missing);

        if (error == ErrorLoop)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

        if (error == ErrorAccess)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);

        return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);
    }

    private static bool IsRegularFile(ushort mode) => (mode & FileTypeMask) == RegularFile;

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

    [StructLayout(LayoutKind.Explicit, Size = StatSize)]
    private struct MacStat
    {
        [FieldOffset(DeviceOffset)]
        public int Device;

        [FieldOffset(ModeOffset)]
        public ushort Mode;

        [FieldOffset(InodeOffset)]
        public ulong Inode;
    }

    [LibraryImport("libSystem.dylib", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenAt(int directory, string path, int flags);

    [LibraryImport("libSystem.dylib", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int file);

    [LibraryImport("libSystem.dylib", EntryPoint = FileStatEntryPoint, SetLastError = true)]
    private static partial int FileStat(int file, out MacStat stat);

    [LibraryImport("libSystem.dylib", EntryPoint = FileStatAtEntryPoint, SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int FileStatAt(int directory, string path, out MacStat stat, int flags);

    [LibraryImport("libSystem.dylib", EntryPoint = "lseek", SetLastError = true)]
    private static partial long LSeek(int file, long offset, int whence);

    [LibraryImport("libSystem.dylib", EntryPoint = "pread", SetLastError = true)]
    private static partial nint PRead(int file, byte[] buffer, nuint count, long offset);

    [LibraryImport("libSystem.dylib", EntryPoint = "unlinkat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int UnlinkAt(int directory, string path, int flags);
}
