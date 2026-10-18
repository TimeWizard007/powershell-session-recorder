using System.Text.Json;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        using var directory = new TempDirectory();
        var store = Store(directory);
        var settings = new ApplicationSettings
        {
            Language = "pl-PL",
            LastLogDirectory = "/var/log/customers",
            LastPowerShellVersion = PowerShellVersion.WindowsPowerShell51,
            IncludeDateAndTime = false,
            SaveSessionInHistory = false
        };
        settings.RememberCustomerTag("contoso");
        settings.RememberCustomerTag("Gdańsk");

        store.Save(settings);
        var loaded = store.LoadDetailed();

        Assert.Equal(SettingsLoadStatus.Loaded, loaded.Status);
        Assert.Equal("pl-PL", loaded.Settings.Language);
        Assert.Equal("/var/log/customers", loaded.Settings.LastLogDirectory);
        Assert.Equal(PowerShellVersion.WindowsPowerShell51, loaded.Settings.LastPowerShellVersion);
        Assert.False(loaded.Settings.IncludeDateAndTime);
        Assert.False(loaded.Settings.SaveSessionInHistory);
        Assert.Equal(["Gdańsk", "contoso"], loaded.Settings.RecentCustomerTags);
        Assert.Contains("\"WindowsPowerShell51\"", File.ReadAllText(store.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Save_WritesOnlyPreferenceFields()
    {
        using var directory = new TempDirectory();
        var store = Store(directory);
        store.Save(new ApplicationSettings());

        using var document = JsonDocument.Parse(File.ReadAllText(store.FilePath));
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.Equal(
            [
                "includeDateAndTime",
                "language",
                "lastLogDirectory",
                "lastPowerShellVersion",
                "recentCustomerTags",
                "saveSessionInHistory",
                "schemaVersion"
            ],
            names);
        Assert.DoesNotContain("password", File.ReadAllText(store.FilePath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingFileReturnsDefaultsAndDoesNotCreateAFile()
    {
        using var directory = new TempDirectory();
        var store = Store(directory);

        var loaded = store.LoadDetailed();

        Assert.Equal(SettingsLoadStatus.Missing, loaded.Status);
        Assert.Equal("en-US", loaded.Settings.Language);
        Assert.Equal(PowerShellVersion.PowerShell7, loaded.Settings.LastPowerShellVersion);
        Assert.True(loaded.Settings.IncludeDateAndTime);
        Assert.True(loaded.Settings.SaveSessionInHistory);
        Assert.Empty(loaded.Settings.RecentCustomerTags);
        Assert.False(File.Exists(store.FilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{ "language": 12 }""")]
    [InlineData("""{ "language": "pl-PL", "saveSessionInHistory": false, "lastPowerShellVersion": "Nope" }""")]
    public void Load_InvalidSettingsReturnDefaultsAndPreserveTheOriginalFile(string json)
    {
        using var directory = new TempDirectory();
        var store = Store(directory);
        File.WriteAllText(store.FilePath, json);

        var loaded = store.LoadDetailed();

        Assert.Equal(SettingsLoadStatus.Invalid, loaded.Status);
        Assert.Equal("en-US", loaded.Settings.Language);
        Assert.True(loaded.Settings.SaveSessionInHistory);
        Assert.Equal(PowerShellVersion.PowerShell7, loaded.Settings.LastPowerShellVersion);
        Assert.Equal(json, File.ReadAllText(store.FilePath));
        Assert.True(File.Exists(store.InvalidBackupPath));
        Assert.Equal(json, File.ReadAllText(store.InvalidBackupPath));
    }

    [Fact]
    public void Load_DoesNotOverwriteAnExistingInvalidBackup()
    {
        using var directory = new TempDirectory();
        var store = Store(directory);
        File.WriteAllText(store.InvalidBackupPath, "first");
        File.WriteAllText(store.FilePath, "{");

        var loaded = store.LoadDetailed();

        Assert.Equal(SettingsLoadStatus.Invalid, loaded.Status);
        Assert.Equal("first", File.ReadAllText(store.InvalidBackupPath));
    }

    [Fact]
    public void Load_RepairsUnknownLanguageAndUndefinedVersion()
    {
        using var directory = new TempDirectory();
        var store = Store(directory);
        File.WriteAllText(
            store.FilePath,
            """
            {
              "language": "fr-FR",
              "lastPowerShellVersion": 99,
              "recentCustomerTags": [" Contoso ", "", "contoso", "Fabrikam"]
            }
            """);

        var loaded = store.LoadDetailed();

        Assert.Equal(SettingsLoadStatus.Loaded, loaded.Status);
        Assert.Equal("en-US", loaded.Settings.Language);
        Assert.Equal(PowerShellVersion.PowerShell7, loaded.Settings.LastPowerShellVersion);
        Assert.Equal(["Contoso", "Fabrikam"], loaded.Settings.RecentCustomerTags);

        var rewritten = store.LoadDetailed();
        Assert.Equal(SettingsLoadStatus.Loaded, rewritten.Status);
        Assert.Equal("en-US", rewritten.Settings.Language);
    }

    [Fact]
    public void Load_AcceptsLanguageAliasesAndTrailingCommas()
    {
        using var directory = new TempDirectory();
        var store = Store(directory);
        File.WriteAllText(store.FilePath, """{ "language": "pl", }""");

        var loaded = store.LoadDetailed();

        Assert.Equal(SettingsLoadStatus.Loaded, loaded.Status);
        Assert.Equal("pl-PL", loaded.Settings.Language);
    }

    [Fact]
    public void RememberCustomerTag_KeepsTheNewestTenTagsWithoutDuplicates()
    {
        var settings = new ApplicationSettings();
        for (var index = 0; index < 12; index++)
            settings.RememberCustomerTag($"TAG{index}");

        settings.RememberCustomerTag("tag11");

        Assert.Equal(10, settings.RecentCustomerTags.Count);
        Assert.Equal("tag11", settings.RecentCustomerTags[0]);
        Assert.Equal("TAG10", settings.RecentCustomerTags[1]);
        Assert.DoesNotContain("TAG0", settings.RecentCustomerTags);
        Assert.DoesNotContain("TAG1", settings.RecentCustomerTags);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RememberCustomerTag_IgnoresBlankValues(string? tag)
    {
        var settings = new ApplicationSettings();

        settings.RememberCustomerTag(tag);

        Assert.Empty(settings.RecentCustomerTags);
    }

    [Fact]
    public void SettingsPath_UsesThePlatformApplicationDirectory()
    {
        Assert.Equal(
            Path.Combine(@"C:\Users\admin\AppData\Roaming", "PowerShellSessionRecorder"),
            SettingsPathResolver.GetSettingsDirectory(
                SettingsPlatform.Windows,
                homeDirectory: null,
                xdgConfigHome: null,
                windowsApplicationData: @"C:\Users\admin\AppData\Roaming"));

        Assert.Equal(
            Path.Combine("/Users/admin/Library/Application Support/PowerShellSessionRecorder"),
            SettingsPathResolver.GetSettingsDirectory(
                SettingsPlatform.MacOS,
                "/Users/admin",
                xdgConfigHome: null,
                windowsApplicationData: null));

        Assert.Equal(
            "/home/admin/.config/powershell-session-recorder",
            SettingsPathResolver.GetSettingsDirectory(
                SettingsPlatform.Linux,
                "/home/admin",
                xdgConfigHome: null,
                windowsApplicationData: null));

        Assert.Equal(
            "/custom/config/powershell-session-recorder/settings.json",
            SettingsPathResolver.GetSettingsFilePath(
                SettingsPlatform.Linux,
                homeDirectory: null,
                xdgConfigHome: "/custom/config",
                windowsApplicationData: null));
    }

    [Fact]
    public void SettingsPath_RequiresAHomeDirectoryWhenXdgConfigHomeIsMissing()
    {
        Assert.Throws<InvalidOperationException>(() => SettingsPathResolver.GetSettingsDirectory(
            SettingsPlatform.Linux,
            homeDirectory: " ",
            xdgConfigHome: null,
            windowsApplicationData: null));
    }

    private static JsonSettingsStore Store(TempDirectory directory)
        => new(Path.Combine(directory.DirectoryPath, "settings.json"));

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            DirectoryPath = Directory.CreateTempSubdirectory("psr-settings-").FullName;
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
