using System.Diagnostics;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class RecordingBootstrapTests
{
    [Fact]
    public void Script_StartsAndStopsTranscriptWithoutDynamicExecution()
    {
        var script = RecordingBootstrap.Script;

        Assert.Contains("Start-Transcript -LiteralPath $TranscriptPath", script, StringComparison.Ordinal);
        Assert.Contains("Stop-Transcript", script, StringComparison.Ordinal);
        Assert.Contains("[Environment]::Exit(1)", script, StringComparison.Ordinal);
        Assert.Contains("RECORDING DID NOT START", script, StringComparison.Ordinal);
        Assert.Contains("REJESTROWANIE NIE ZOSTAŁO URUCHOMIONE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", script, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\biex\b", script);
    }

    [Theory]
    [InlineData("/tmp/O'Brien/Gdańsk.txt")]
    [InlineData("/tmp/customer/'; Remove-Item -Recurse.txt")]
    [InlineData("/tmp/customer/$(Invoke-Expression 'whoami').txt")]
    public void BuildPowerShellArguments_PassesTheTranscriptPathAsOneArgument(string transcriptPath)
    {
        var arguments = RecordingBootstrap.BuildPowerShellArguments(
            "/home/mwi",
            "/tmp/bootstrap.ps1",
            transcriptPath,
            "pl-PL");

        Assert.Equal("-NoLogo", arguments[0]);
        Assert.Contains("-NoProfile", arguments);
        Assert.Contains("-NoExit", arguments);
        Assert.Contains("-File", arguments);
        Assert.DoesNotContain("-Command", arguments);
        Assert.DoesNotContain("-EncodedCommand", arguments);
        Assert.Equal(transcriptPath, arguments[arguments.ToList().IndexOf("-TranscriptPath") + 1]);
        Assert.Equal(1, arguments.Count(argument => argument == transcriptPath));
        Assert.DoesNotContain(RecordingBootstrap.Script, transcriptPath, StringComparison.Ordinal);
        Assert.DoesNotContain(transcriptPath, RecordingBootstrap.Script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pl-PL", "pl-PL")]
    [InlineData("en-US", "en-US")]
    [InlineData("fr-FR", "en-US")]
    [InlineData(null, "en-US")]
    public void BuildPowerShellArguments_AllowsOnlyKnownLanguages(string? language, string expected)
    {
        var arguments = RecordingBootstrap.BuildPowerShellArguments("/tmp", "/tmp/b.ps1", "/tmp/t.txt", language);
        Assert.Equal(expected, arguments[arguments.ToList().IndexOf("-Language") + 1]);
    }

    [Fact]
    public void PowerShell_RecordsAndClosesTheTranscript()
    {
        var pwsh = FindPowerShell();
        if (pwsh is null)
            return;

        using var root = new TempDirectory();
        var transcript = Path.Combine(root.Path, "O'Brien Gdańsk.txt");
        var bootstrap = WriteBootstrap(root.Path);
        File.WriteAllText(transcript, "");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                transcript,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead);
        }

        var (_, output) = RunPowerShell(pwsh!, bootstrap, transcript, "en-US", "Get-Date\nGet-Location\nexit\n");

        var body = File.ReadAllText(transcript);
        Assert.Contains("Recording is active.", output, StringComparison.Ordinal);
        Assert.Contains(transcript, output, StringComparison.Ordinal);
        Assert.Contains("transcript start", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-Date", body, StringComparison.Ordinal);
        Assert.Contains("Get-Location", body, StringComparison.Ordinal);
        Assert.Contains("transcript end", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(bootstrap));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(transcript));
    }

    [Fact]
    public void PowerShell_DoesNotLeaveAnUnrecordedShellWhenTranscriptFails()
    {
        var pwsh = FindPowerShell();
        if (pwsh is null)
            return;

        using var root = new TempDirectory();
        var blocker = Path.Combine(root.Path, "not-a-directory");
        File.WriteAllText(blocker, "keep");
        var transcript = Path.Combine(blocker, "session.txt");
        var bootstrap = WriteBootstrap(root.Path);

        var (exitCode, output) = RunPowerShell(pwsh!, bootstrap, transcript, "pl-PL", "");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("REJESTROWANIE NIE ZOSTAŁO URUCHOMIONE", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Rejestrowanie jest aktywne.", output, StringComparison.Ordinal);
        Assert.Equal("keep", File.ReadAllText(blocker));
        Assert.False(File.Exists(bootstrap));
    }

    private static string? FindPowerShell()
        => ExecutableLocator.Find("pwsh", Environment.GetEnvironmentVariable("PATH"), File.Exists);

    private static string WriteBootstrap(string directory)
    {
        var path = Path.Combine(directory, "bootstrap.ps1");
        File.WriteAllText(path, RecordingBootstrap.Script);
        return path;
    }

    private static (int ExitCode, string Output) RunPowerShell(
        string pwsh,
        string bootstrap,
        string transcript,
        string language,
        string stdin)
    {
        var start = new ProcessStartInfo
        {
            FileName = pwsh,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NoExit");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(bootstrap);
        start.ArgumentList.Add("-TranscriptPath");
        start.ArgumentList.Add(transcript);
        start.ArgumentList.Add("-Language");
        start.ArgumentList.Add(language);
        start.ArgumentList.Add("-BootstrapPath");
        start.ArgumentList.Add(bootstrap);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("pwsh did not start.");
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();
        if (!process.WaitForExit(20_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            throw new TimeoutException(process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
        }

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        return (process.ExitCode, output);
    }
}
