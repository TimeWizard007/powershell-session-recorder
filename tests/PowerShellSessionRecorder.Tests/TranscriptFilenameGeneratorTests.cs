using System.Globalization;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class TranscriptFilenameGeneratorTests
{
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    private static readonly char[] ForbiddenCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    [Fact]
    public void SanitizeSegment_ReplacesWindowsInvalidCharacters()
    {
        var sanitized = TranscriptFilenameGenerator.SanitizeSegment("a<b>c:d\"e/f\\g|h?i*j");

        Assert.Equal("a_b_c_d_e_f_g_h_i_j", sanitized);
        AssertWindowsSafe(sanitized + ".txt");
    }

    [Theory]
    [InlineData("a\nb", "a_b")]
    [InlineData("a\tb", "a_b")]
    [InlineData("a\0b", "a_b")]
    [InlineData("a\u0007b", "a_b")]
    public void SanitizeSegment_ReplacesControlCharacters(string input, string expected)
    {
        Assert.Equal(expected, TranscriptFilenameGenerator.SanitizeSegment(input));
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("con", "_con")]
    [InlineData("PRN", "_PRN")]
    [InlineData("AUX", "_AUX")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1", "_COM1")]
    [InlineData("com1.txt", "_com1.txt")]
    [InlineData("LPT1", "_LPT1")]
    [InlineData("lpt9", "_lpt9")]
    [InlineData("COM0", "_COM0")]
    [InlineData("LPT0", "_LPT0")]
    public void SanitizeSegment_PrefixesWindowsReservedNames(string input, string expected)
    {
        var sanitized = TranscriptFilenameGenerator.SanitizeSegment(input);

        Assert.Equal(expected, sanitized);
        Assert.DoesNotContain(
            ReservedNames,
            name => name.Equals(sanitized.Split('.')[0], StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SanitizeSegment_PrefixesSuperscriptDeviceNames()
    {
        Assert.Equal("_COM\u00B9", TranscriptFilenameGenerator.SanitizeSegment("COM\u00B9"));
        Assert.Equal("_LPT\u00B3", TranscriptFilenameGenerator.SanitizeSegment("LPT\u00B3"));
    }

    [Theory]
    [InlineData(" report. ", "report")]
    [InlineData("Invoice...", "Invoice")]
    [InlineData("My   Session", "My_Session")]
    [InlineData("___", "")]
    [InlineData("???", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void SanitizeSegment_TrimsDecorationsAndCollapsesWhitespace(string? input, string expected)
    {
        Assert.Equal(expected, TranscriptFilenameGenerator.SanitizeSegment(input));
    }

    [Fact]
    public void SanitizeSegment_PreservesPolishCharacters()
    {
        Assert.Equal("Zażółć_gęślą_jaźń", TranscriptFilenameGenerator.SanitizeSegment("  Zażółć   gęślą jaźń  "));
    }

    [Fact]
    public void SanitizeSegment_LimitsSegmentLength()
    {
        var sanitized = TranscriptFilenameGenerator.SanitizeSegment(new string('a', 80));

        Assert.Equal(TranscriptFilenameGenerator.MaxSegmentLength, sanitized.Length);
    }

    [Fact]
    public void BuildFileName_IncludesTimestampWhenRequested()
    {
        var timestamp = new DateTimeOffset(2026, 10, 18, 14, 5, 9, TimeSpan.FromHours(2));

        var fileName = TranscriptFilenameGenerator.BuildFileName(
            "Contoso",
            "Mailbox fix",
            includeDateAndTime: true,
            timestamp,
            "AB12CD34");

        Assert.Equal("2026-10-18_14-05-09_Contoso_Mailbox_fix_AB12CD34.txt", fileName);
        AssertWindowsSafe(fileName);
    }

    [Fact]
    public void BuildFileName_OmitsTimestampWhenDisabled()
    {
        var fileName = TranscriptFilenameGenerator.BuildFileName(
            "Contoso",
            "Mailbox fix",
            includeDateAndTime: false,
            new DateTimeOffset(2026, 10, 18, 14, 5, 9, TimeSpan.Zero),
            "AB12CD34");

        Assert.Equal("Contoso_Mailbox_fix_AB12CD34.txt", fileName);
    }

    [Fact]
    public void BuildFileName_UsesInvariantFormattingRegardlessOfCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var fileName = TranscriptFilenameGenerator.BuildFileName(
                "Gdańsk",
                "Sesja",
                includeDateAndTime: true,
                new DateTimeOffset(2026, 10, 18, 7, 8, 9, TimeSpan.Zero),
                "AA11BB22");

            Assert.Equal("2026-10-18_07-08-09_Gdańsk_Sesja_AA11BB22.txt", fileName);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("", "Repair")]
    [InlineData("   ", "Repair")]
    [InlineData("???", "Repair")]
    [InlineData("Contoso", "")]
    [InlineData("Contoso", "   ")]
    [InlineData("Contoso", ":::")]
    public void BuildFileName_RejectsEmptySegments(string tag, string sessionName)
    {
        Assert.Throws<ArgumentException>(() => TranscriptFilenameGenerator.BuildFileName(
            tag,
            sessionName,
            includeDateAndTime: false,
            DateTimeOffset.UnixEpoch,
            "AB12CD34"));
    }

    [Fact]
    public void CreateUniqueFileName_SkipsNamesThatAlreadyExist()
    {
        var directory = OperatingSystem.IsWindows() ? @"C:\Transcripts" : "/var/transcripts";
        var ids = new Queue<string>(["AAAAAAAA", "AAAAAAAA", "BBBBBBBB"]);
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? firstSeenPath = null;

        var first = TranscriptFilenameGenerator.CreateUniqueFileName(
            directory,
            "Contoso",
            "Repair",
            includeDateAndTime: false,
            DateTimeOffset.UnixEpoch,
            () => ids.Dequeue(),
            path =>
            {
                firstSeenPath ??= path;
                return existing.Contains(path);
            });

        existing.Add(Path.Combine(directory, first));

        var second = TranscriptFilenameGenerator.CreateUniqueFileName(
            directory,
            "Contoso",
            "Repair",
            includeDateAndTime: false,
            DateTimeOffset.UnixEpoch,
            () => ids.Dequeue(),
            existing.Contains);

        Assert.Equal("Contoso_Repair_AAAAAAAA.txt", first);
        Assert.Equal("Contoso_Repair_BBBBBBBB.txt", second);
        Assert.Equal(Path.Combine(directory, first), firstSeenPath);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateUniqueFileName_DoesNotReturnAnExistingFileOnDisk()
    {
        var directory = Directory.CreateTempSubdirectory("psr-filename-").FullName;
        try
        {
            var first = TranscriptFilenameGenerator.CreateUniqueFileName(
                directory,
                "Contoso",
                "Repair",
                false,
                DateTimeOffset.UnixEpoch,
                () => "AAAAAAAA",
                File.Exists);
            File.WriteAllText(Path.Combine(directory, first), "existing");

            var candidateIds = new Queue<string>(["AAAAAAAA", "BBBBBBBB"]);
            var second = TranscriptFilenameGenerator.CreateUniqueFileName(
                directory,
                "Contoso",
                "Repair",
                false,
                DateTimeOffset.UnixEpoch,
                () => candidateIds.Dequeue(),
                File.Exists);

            Assert.True(File.Exists(Path.Combine(directory, first)));
            Assert.False(File.Exists(Path.Combine(directory, second)));
            Assert.NotEqual(first, second);
            AssertWindowsSafe(second);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateUniqueFileName_ThrowsWhenEveryCandidateExists()
    {
        Assert.Throws<InvalidOperationException>(() => TranscriptFilenameGenerator.CreateUniqueFileName(
            "/tmp",
            "Contoso",
            "Repair",
            false,
            DateTimeOffset.UnixEpoch,
            () => "AAAAAAAA",
            _ => true));
    }

    private static void AssertWindowsSafe(string fileName)
    {
        Assert.False(string.IsNullOrWhiteSpace(fileName));
        Assert.EndsWith(".txt", fileName, StringComparison.Ordinal);
        Assert.Equal(fileName, Path.GetFileName(fileName));
        Assert.DoesNotContain(fileName, character => character < 32 || ForbiddenCharacters.Contains(character));
        Assert.False(fileName.EndsWith(' ') || fileName.EndsWith('.'));

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var baseName = stem.Split('.')[0];
        Assert.DoesNotContain(
            ReservedNames,
            name => name.Equals(baseName, StringComparison.OrdinalIgnoreCase));
    }
}
