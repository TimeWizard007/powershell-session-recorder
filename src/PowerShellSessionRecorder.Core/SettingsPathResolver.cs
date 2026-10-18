namespace PowerShellSessionRecorder.Core;

public enum SettingsPlatform
{
    Windows,
    MacOS,
    Linux
}

/// <summary>
/// Resolves the per-user settings directory without reading the live operating system,
/// so the rules can be tested on any host.
/// </summary>
public static class SettingsPathResolver
{
    public const string WindowsFolderName = "PowerShellSessionRecorder";

    public const string MacFolderName = "PowerShellSessionRecorder";

    public const string LinuxFolderName = "powershell-session-recorder";

    public const string SettingsFileName = "settings.json";

    public static string GetSettingsDirectory(
        SettingsPlatform platform,
        string? homeDirectory,
        string? xdgConfigHome,
        string? windowsApplicationData)
    {
        return platform switch
        {
            SettingsPlatform.Windows => Path.Combine(
                Require(windowsApplicationData, "Windows application data directory"),
                WindowsFolderName),
            SettingsPlatform.MacOS => Path.Combine(
                Require(homeDirectory, "Home directory"),
                "Library",
                "Application Support",
                MacFolderName),
            _ => Path.Combine(ResolveLinuxConfigRoot(homeDirectory, xdgConfigHome), LinuxFolderName)
        };
    }

    public static string GetSettingsFilePath(
        SettingsPlatform platform,
        string? homeDirectory,
        string? xdgConfigHome,
        string? windowsApplicationData)
        => Path.Combine(
            GetSettingsDirectory(platform, homeDirectory, xdgConfigHome, windowsApplicationData),
            SettingsFileName);

    private static string ResolveLinuxConfigRoot(string? homeDirectory, string? xdgConfigHome)
    {
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
            return xdgConfigHome.Trim();

        return Path.Combine(Require(homeDirectory, "Home directory"), ".config");
    }

    private static string Require(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{description} is not available.");

        return value.Trim();
    }
}
