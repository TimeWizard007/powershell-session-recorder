using PowerShellSessionRecorder.App.Services.Recording;

namespace PowerShellSessionRecorder.Tests;

public class GnomeTerminalCommandTests
{
    [Fact]
    public void BuildArguments_PlacesTheCommandAfterADoubleDash()
    {
        var arguments = GnomeTerminalCommand.BuildArguments(
            "PowerShell Session Recorder — HOWE",
            ["/usr/bin/pwsh", "-NoLogo", "-NoProfile", "-File", "/tmp/bootstrap.ps1"]);

        Assert.Equal("--title", arguments[0]);
        Assert.Equal("--", arguments[2]);
        Assert.Equal("/usr/bin/pwsh", arguments[3]);
        Assert.DoesNotContain("-e", arguments);
        Assert.DoesNotContain("sh", arguments);
        Assert.DoesNotContain("-c", arguments);
    }

    [Fact]
    public void SanitizeTitle_RemovesControlCharactersAndLeadingDashes()
    {
        Assert.Equal("AB", GnomeTerminalCommand.SanitizeTitle("A\nB\t"));
        Assert.StartsWith("PowerShell Session Recorder ", GnomeTerminalCommand.SanitizeTitle("-injected"));
        Assert.Equal("PowerShell Session Recorder", GnomeTerminalCommand.SanitizeTitle("   "));
    }
}
