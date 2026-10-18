namespace PowerShellSessionRecorder.Core;

/// <summary>
/// PowerShell edition that a future session will launch.
/// Windows PowerShell 5.1 is available only on Windows.
/// </summary>
public enum PowerShellVersion
{
    PowerShell7 = 0,
    WindowsPowerShell51 = 1
}
