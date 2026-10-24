namespace PowerShellSessionRecorder.Core;

public sealed record PowerShellInstallInspection(
    PowerShellVersion Family,
    string Path,
    string? ProductVersion,
    bool UsedForRecording,
    string? ResolvedPath = null);

/// <summary>
/// The fixed PowerShell command that prints the installed version. It contains no session data.
/// </summary>
public static class PowerShellVersionCommand
{
    public static IReadOnlyList<string> Arguments { get; } =
    [
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        "$PSVersionTable.PSVersion.ToString()"
    ];
}
