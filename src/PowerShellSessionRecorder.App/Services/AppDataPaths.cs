using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services;

public static class AppDataPaths
{
    public static string GetSettingsFilePath()
    {
        var platform = OperatingSystem.IsWindows()
            ? SettingsPlatform.Windows
            : OperatingSystem.IsMacOS()
                ? SettingsPlatform.MacOS
                : SettingsPlatform.Linux;

        return SettingsPathResolver.GetSettingsFilePath(
            platform,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    public static string GetLocalizationDirectory()
        => Path.Combine(AppContext.BaseDirectory, "Localization");
}
