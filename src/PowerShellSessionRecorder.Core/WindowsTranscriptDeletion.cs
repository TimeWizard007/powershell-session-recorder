using System.Runtime.InteropServices;
using System.Text;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Deletes one regular transcript on Windows 10 or later.
/// The handle is opened without following a reparse point. Backup semantics let a
/// directory be classified as unsafe; the directory itself is not deleted.
/// A sharing violation or a missing disposition API refuses the delete.
/// </summary>
internal static partial class WindowsTranscriptDeletion
{
    private const uint GenericRead = 0x80000000;
    private const uint DeleteAccess = 0x00010000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint FileBegin = 0;
    // FILE_INFO_BY_HANDLE_CLASS.FileDispositionInfoEx. 64 is the unrelated NT class.
    internal const int FileDispositionInfoExClass = 21;
    private const uint DispositionDelete = 0x1;
    private const uint DispositionPosix = 0x2;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;

    private static readonly IntPtr InvalidHandle = new(-1);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static TranscriptDeleteOutcome Delete(string fullPath)
    {
        var handle = CreateFile(
            fullPath,
            GenericRead | DeleteAccess,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle == InvalidHandle || handle == IntPtr.Zero)
            return MapOpenError(Marshal.GetLastPInvokeError());

        try
        {
            if (GetFileInformationByHandle(handle, out var information) == 0)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);

            var reparse = (information.FileAttributes & FileAttributeReparsePoint) != 0;
            var directory = (information.FileAttributes & FileAttributeDirectory) != 0;
            if (TranscriptDeletionRules.Classify(false, reparse, directory, regularFile: !reparse && !directory, footer: null, readFailed: false)
                == TranscriptDeleteStatus.Unsafe)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

            var tail = ReadTail(handle, information);
            if (tail is null)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);

            var footer = TranscriptStatusProbe.ObserveTail(tail).Status;
            var decision = TranscriptDeletionRules.Classify(
                missing: false,
                reparsePoint: reparse,
                directory: directory,
                regularFile: !reparse && !directory,
                footer: footer,
                readFailed: false);
            if (decision != TranscriptDeleteStatus.Deleted)
                return new TranscriptDeleteOutcome(decision);

            if (GetFileInformationByHandle(handle, out var again) == 0
                || (again.FileAttributes & (FileAttributeReparsePoint | FileAttributeDirectory)) != 0
                || again.VolumeSerialNumber != information.VolumeSerialNumber
                || again.FileIndexHigh != information.FileIndexHigh
                || again.FileIndexLow != information.FileIndexLow)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Unsafe);

            var disposition = new FileDispositionInfoEx
            {
                Flags = DispositionDelete | DispositionPosix
            };
            if (SetFileInformationByHandle(handle, FileDispositionInfoExClass, ref disposition, Marshal.SizeOf<FileDispositionInfoEx>()) == 0)
                return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);

            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Deleted);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    private static TranscriptDeleteOutcome MapOpenError(int error)
    {
        if (error is ErrorFileNotFound or ErrorPathNotFound)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Missing);
        if (error is ErrorAccessDenied or ErrorSharingViolation)
            return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);
        return new TranscriptDeleteOutcome(TranscriptDeleteStatus.Failed);
    }

    private static string? ReadTail(IntPtr handle, ByHandleFileInformation information)
    {
        var size = ((long)information.FileSizeHigh << 32) | information.FileSizeLow;
        if (size < 0)
            return null;
        if (size == 0)
            return "";

        var length = size > TranscriptStatusProbe.TailBytes ? TranscriptStatusProbe.TailBytes : (int)size;
        var start = size - length;
        if (SetFilePointerEx(handle, start, out _, FileBegin) == 0)
            return null;

        var buffer = new byte[length];
        if (ReadFile(handle, buffer, length, out var read, IntPtr.Zero) == 0 || read < 0)
            return null;

        return Utf8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// Matches Win32 BY_HANDLE_FILE_INFORMATION. The volume serial number precedes the size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfoEx
    {
        public uint Flags;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int CloseHandle(IntPtr handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetFileInformationByHandle(IntPtr file, out ByHandleFileInformation information);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int ReadFile(IntPtr file, byte[] buffer, int count, out int read, IntPtr overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int SetFilePointerEx(IntPtr file, long distance, out long newPointer, uint moveMethod);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int SetFileInformationByHandle(
        IntPtr file,
        int fileInformationClass,
        ref FileDispositionInfoEx information,
        int size);
}
