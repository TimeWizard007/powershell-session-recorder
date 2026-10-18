namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Draft of a recorded PowerShell session. Checkpoint 1 stores and previews this
/// configuration; it does not launch PowerShell.
/// </summary>
public sealed class SessionConfiguration
{
    public string CustomerTag { get; set; } = "";

    public string SessionName { get; set; } = "";

    public string LogDirectory { get; set; } = "";

    public PowerShellVersion PowerShellVersion { get; set; } = PowerShellVersion.PowerShell7;

    public bool RunAsAdministrator { get; set; }

    public bool IncludeDateAndTime { get; set; } = true;

    public bool SaveSessionInHistory { get; set; } = true;
}
