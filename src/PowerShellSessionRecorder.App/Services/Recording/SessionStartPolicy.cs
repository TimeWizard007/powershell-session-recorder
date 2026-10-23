using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services.Recording;

public static class SessionStartPolicy
{
    public static bool CanStart(
        bool windows,
        SessionRuntimeCapabilities capabilities,
        PowerShellVersion version,
        bool runAsAdministrator,
        bool configurationValid,
        bool hasLogDirectory,
        bool logDirectoryIsFile)
    {
        if (!configurationValid || !hasLogDirectory || logDirectoryIsFile || !capabilities.PlatformSupported)
            return false;

        if (!windows)
        {
            return !runAsAdministrator
                && version == PowerShellVersion.PowerShell7
                && capabilities.PowerShell7Available
                && capabilities.TerminalAvailable;
        }

        var engineAvailable = version switch
        {
            PowerShellVersion.PowerShell7 => capabilities.PowerShell7Available,
            PowerShellVersion.WindowsPowerShell51 => capabilities.WindowsPowerShellAvailable,
            _ => false
        };
        if (!engineAvailable)
            return false;

        return !runAsAdministrator || capabilities.ElevationAvailable;
    }
}
