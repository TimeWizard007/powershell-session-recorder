namespace PowerShellSessionRecorder.Core.Updates;

/// <summary>
/// Which published GitHub releases may be offered. The default is the stable channel.
/// </summary>
public enum UpdateChannel
{
    Stable = 0,
    Beta = 1
}
