using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class TranscriptFileAllocatorTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Allocate_CreatesAMissingDirectoryAndAnEmptyPrivateFile()
    {
        using var root = new TempDirectory();
        var directory = Path.Combine(root.Path, "Gdańsk", "O'Brien Session");
        var allocator = new TranscriptFileAllocator();

        var path = allocator.Allocate(directory, "Gdańsk", "O'Brien Session", false, Stamp, () => "ABCDEF12");

        Assert.True(Directory.Exists(directory));
        Assert.True(File.Exists(path));
        Assert.Equal(0, new FileInfo(path).Length);
        Assert.Contains("Gdańsk_O'Brien_Session_ABCDEF12.txt", path, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void Allocate_DoesNotOverwriteAnExistingTranscript()
    {
        using var root = new TempDirectory();
        var existingName = TranscriptFilenameGenerator.BuildFileName("HOWE", "Diagnostyka VPN", true, Stamp, "AAAAAAAA");
        var existingPath = Path.Combine(root.Path, existingName);
        File.WriteAllText(existingPath, "keep");
        var ids = new Queue<string>(["AAAAAAAA", "BBBBBBBB"]);

        var path = new TranscriptFileAllocator().Allocate(
            root.Path,
            "HOWE",
            "Diagnostyka VPN",
            true,
            Stamp,
            () => ids.Dequeue());

        Assert.EndsWith("BBBBBBBB.txt", path, StringComparison.Ordinal);
        Assert.Equal("keep", File.ReadAllText(existingPath));
        Assert.Equal(0, new FileInfo(path).Length);
    }

    [Fact]
    public void Allocate_GivesSimultaneousSessionsDifferentFiles()
    {
        using var root = new TempDirectory();
        var next = 0;
        var allocator = new TranscriptFileAllocator();

        var paths = Enumerable.Range(0, 12)
            .AsParallel()
            .Select(_ => allocator.Allocate(
                root.Path,
                "HOWE",
                "Diagnostyka VPN",
                false,
                Stamp,
                () => Interlocked.Increment(ref next).ToString("X16")))
            .ToArray();

        Assert.Equal(12, paths.Distinct(StringComparer.Ordinal).Count());
        Assert.All(paths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void Allocate_ThrowsWhenTheDirectoryCannotBeCreated()
    {
        using var root = new TempDirectory();
        var blocker = Path.Combine(root.Path, "not-a-directory");
        File.WriteAllText(blocker, "keep");

        var exception = Assert.ThrowsAny<Exception>(() => new TranscriptFileAllocator().Allocate(
            Path.Combine(blocker, "logs"),
            "HOWE",
            "Session",
            false,
            Stamp,
            () => "ABCDEF12"));

        Assert.True(exception is IOException or UnauthorizedAccessException);
        Assert.Equal("keep", File.ReadAllText(blocker));
    }

    [Fact]
    public void Allocate_ThrowsWhenTheDirectoryIsNotWritable()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var root = new TempDirectory();
        var locked = Path.Combine(root.Path, "locked");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            try
            {
                File.WriteAllText(Path.Combine(locked, "probe.txt"), "x");
                return;
            }
            catch (UnauthorizedAccessException)
            {
            }

            var exception = Assert.ThrowsAny<Exception>(() => new TranscriptFileAllocator().Allocate(
                locked,
                "HOWE",
                "Session",
                false,
                Stamp,
                () => "ABCDEF12"));
            Assert.True(exception is UnauthorizedAccessException or IOException);
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
