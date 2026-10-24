namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Facts about the recording host. Linux uses GNOME Terminal, macOS uses Terminal.app,
/// and Windows launches the PowerShell executable directly.
/// </summary>
public static class RecordingPlatform
{
    public const string PowerShellExecutable = "pwsh";

    public const string GnomeTerminalExecutable = "gnome-terminal";

    public const string PowerShellInstallDocumentation =
        "https://learn.microsoft.com/powershell/scripting/install/installing-powershell";

    public const string RepositoryUrl =
        "https://github.com/TimeWizard007/powershell-session-recorder";

    public const string UbuntuTerminalInstallCommand = "sudo apt install gnome-terminal";

    public static bool IsCurrentPlatformSupported()
        => OperatingSystem.IsLinux() || OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
}
