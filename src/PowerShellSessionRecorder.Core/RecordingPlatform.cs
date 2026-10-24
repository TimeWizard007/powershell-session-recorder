namespace PowerShellSessionRecorder.Core;

/// <summary>
/// The host whose launch and hint behavior a caller wants. Production uses
/// <see cref="RecordingPlatform.CurrentHost"/>. Tests pass an explicit value so
/// Linux and macOS behavior can be checked on any operating system.
/// </summary>
public enum RecordingHost
{
    Linux,
    Mac,
    Windows
}

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

    public static RecordingHost CurrentHost()
        => OperatingSystem.IsWindows()
            ? RecordingHost.Windows
            : OperatingSystem.IsMacOS()
                ? RecordingHost.Mac
                : RecordingHost.Linux;
}
