using System.Runtime.InteropServices;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.Services.Updates;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint6Tests
{
    [Fact]
    public void MacDiscovery_ResolvesHomebrewAndKeepsTheOfficialInstallFirst()
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/opt/homebrew/bin/pwsh"] = "../Cellar/powershell/7.6.8/bin/pwsh"
        };
        var regular = new HashSet<string>(StringComparer.Ordinal)
        {
            "/usr/local/microsoft/powershell/7/pwsh",
            "/opt/homebrew/Cellar/powershell/7.6.8/bin/pwsh"
        };
        var found = MacPowerShellDiscovery.Discover(
            "/tmp/cwd" + Path.PathSeparator + "/opt/homebrew/bin",
            "/tmp/cwd",
            path => Inspect(path, regular, links),
            path => links.TryGetValue(path, out var target) ? target : null);

        Assert.Equal(
            [
                "/usr/local/microsoft/powershell/7/pwsh",
                "/opt/homebrew/bin/pwsh"
            ],
            found.Select(install => install.DisplayPath).ToArray());
        Assert.Equal("/usr/local/microsoft/powershell/7/pwsh", found[0].ExecutablePath);
        Assert.Equal("/opt/homebrew/Cellar/powershell/7.6.8/bin/pwsh", found[1].ExecutablePath);
    }

    [Fact]
    public void MacDiscovery_RejectsSymlinksThatDoNotResolveToPwsh()
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/usr/local/bin/pwsh"] = "/bin/bash",
            ["/opt/homebrew/bin/pwsh"] = "/opt/homebrew/bin/pwsh"
        };
        var present = new HashSet<string>(StringComparer.Ordinal)
        {
            "/bin/bash",
            "/usr/local/bin/pwsh",
            "/opt/homebrew/bin/pwsh"
        };
        var found = MacPowerShellDiscovery.Discover(
            null,
            null,
            path => Inspect(path, present, links),
            path => links.TryGetValue(path, out var target) ? target : null);

        Assert.Empty(found);
    }

    [Fact]
    public void MacDiscovery_IgnoresRelativePathEntries()
    {
        var regular = new HashSet<string>(StringComparer.Ordinal) { "relative/pwsh" };
        var found = MacPowerShellDiscovery.Discover(
            "relative",
            null,
            path => new EngineFileInspection(regular.Contains(path), false),
            _ => null);

        Assert.Empty(found);
    }

    [Fact]
    public void MacDiscovery_IgnoresPowerShellInsideTheCurrentDirectory()
    {
        var regular = new HashSet<string>(StringComparer.Ordinal)
        {
            "/tmp/cwd/pwsh",
            "/usr/local/custom/bin/pwsh"
        };
        var found = MacPowerShellDiscovery.Discover(
            "/tmp/cwd" + Path.PathSeparator + "/usr/local/custom/bin/.",
            "/tmp/cwd",
            path => new EngineFileInspection(regular.Contains(path), false),
            _ => null);

        Assert.Equal(["/usr/local/custom/bin/pwsh"], found.Select(install => install.DisplayPath).ToArray());
        Assert.Equal("/usr/local/custom/bin/pwsh", found[0].ExecutablePath);
    }

    [Fact]
    public void MacTerminalScript_QuotesUnicodeSpacesAndQuotesWithoutAShell()
    {
        var executable = "/opt/homebrew/Cellar/powershell/7.6.8/bin/pwsh";
        var scriptPath = "/Users/mwi/Sesja łóżka/session.command";
        var arguments = RecordingBootstrap.BuildPowerShellArguments(
            "/Users/mwi/Sesja łóżka",
            "/tmp/bootstrap.ps1",
            "/Users/mwi/Logi/HOWE_$(Invoke-Expression 'whoami').txt",
            "pl-PL");

        Assert.True(MacTerminalCommand.TryBuildScript(executable, arguments, scriptPath, out var script));
        Assert.StartsWith("#!/bin/sh\n", script, StringComparison.Ordinal);
        Assert.Contains("exec '/opt/homebrew/Cellar/powershell/7.6.8/bin/pwsh' ", script, StringComparison.Ordinal);
        Assert.Contains("'/Users/mwi/Sesja łóżka'", script, StringComparison.Ordinal);
        Assert.Contains("'/Users/mwi/Logi/HOWE_$(Invoke-Expression '\\''whoami'\\'').txt'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("gnome-terminal", script, StringComparison.Ordinal);
        Assert.DoesNotContain("osascript", script, StringComparison.Ordinal);
        Assert.Equal(["-a", "Terminal", scriptPath], MacTerminalCommand.OpenArguments(scriptPath));
        Assert.False(MacTerminalCommand.TryBuildScript(executable, arguments, "/tmp/bad\nname.command", out _));
    }

    [Fact]
    public void InstalledPowerShellVersion_ParsesWindowsPowerShellAndRejectsPrerelease()
    {
        Assert.True(PowerShellProductVersion.TryParse("5.1.22621.2506\n", out var windows));
        Assert.Equal(5, windows.Major);
        Assert.Equal(2506, windows.Revision);
        Assert.Equal("7.6.8", Assert.IsType<PowerShellProductVersion>(Parsed("7.6.8")).Text);
        Assert.False(PowerShellProductVersion.TryParse("7.6.0-preview.4", out _));
    }

    [Fact]
    public void PowerShellReleaseCatalog_SelectsTheHighestStable7Release()
    {
        var releases = new PublishedRelease[]
        {
            new("v7.6.0-preview.5", null, false, true),
            new("v6.2.7", null, false, false),
            new("v7.4.11", null, false, false),
            new("v7.5.2", null, true, false),
            new("v7.5.0", null, false, false)
        };

        Assert.True(PowerShellReleaseCatalog.TrySelectHighestStable(releases, out var version));
        Assert.Equal("7.5.0", version);
        Assert.False(PowerShellReleaseCatalog.TrySelectStable(
            new GitHubFetchResult(GitHubFetchStatus.Ok, "v7.6.0-preview.1", null),
            out _));
        Assert.False(PowerShellReleaseCatalog.TrySelectStable(
            new GitHubFetchResult(GitHubFetchStatus.RateLimited, "v7.6.8", null),
            out _));
    }

    [Fact]
    public async Task PowerShellVersions_UseTheCacheAndDoNotInstallAnything()
    {
        using var directory = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.Path, "settings.json"));
        var settings = new ApplicationSettings
        {
            CachedPowerShell7StableVersion = "7.5.0",
            LastPowerShellReleaseCheckUtc = DateTimeOffset.UtcNow
        };
        var releases = new FakePowerShellReleases
        {
            Result = new GitHubFetchResult(GitHubFetchStatus.Ok, "v7.9.0", "https://github.com/PowerShell/PowerShell/releases/tag/v7.9.0")
        };
        var viewModel = CreateVersions(
            store,
            settings,
            [
                new PowerShellInstallInspection(PowerShellVersion.PowerShell7, "/usr/local/microsoft/powershell/7/pwsh", "7.6.8", true),
                new PowerShellInstallInspection(
                    PowerShellVersion.PowerShell7,
                    "/opt/homebrew/bin/pwsh",
                    "7.4.6",
                    false,
                    "/opt/homebrew/Cellar/powershell/7.4.6/bin/pwsh")
            ],
            releases);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, releases.Calls);
        Assert.Equal("7.5.0", viewModel.LatestText);
        Assert.Contains("7.6.8", viewModel.InstalledText, StringComparison.Ordinal);
        Assert.Contains("used for PowerShell 7 recordings", viewModel.InstalledText, StringComparison.Ordinal);
        Assert.Contains("/opt/homebrew/bin/pwsh → /opt/homebrew/Cellar/powershell/7.4.6/bin/pwsh", viewModel.InstalledText, StringComparison.Ordinal);
        Assert.DoesNotContain("7.4.6 — used for PowerShell 7 recordings", viewModel.InstalledText, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", viewModel.InstalledText, StringComparison.Ordinal);
        Assert.Equal("7.5.0", settings.CachedPowerShell7StableVersion);
    }

    [Fact]
    public async Task PowerShellVersions_ReportNotInstalledAndAFailedLatestCheck()
    {
        using var directory = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.Path, "settings.json"));
        var settings = new ApplicationSettings();
        var releases = new FakePowerShellReleases
        {
            Result = new GitHubFetchResult(GitHubFetchStatus.Offline, null, null)
        };
        var viewModel = CreateVersions(store, settings, [], releases);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(1, releases.Calls);
        Assert.Equal("Not installed", viewModel.InstalledText);
        Assert.Equal("Unable to check the latest version", viewModel.LatestText);
        Assert.Equal("", settings.CachedPowerShell7StableVersion);
    }

    [Fact]
    public async Task PowerShellVersions_RememberAStableReleaseAndIgnoreAPreview()
    {
        using var directory = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(directory.Path, "settings.json"));
        var settings = new ApplicationSettings();
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        var releases = new FakePowerShellReleases
        {
            Result = new GitHubFetchResult(GitHubFetchStatus.Ok, "v7.6.0-preview.4", null)
        };
        var viewModel = CreateVersions(store, settings, [], releases, () => now);
        await viewModel.RefreshAsync(CancellationToken.None);
        Assert.Equal("", settings.CachedPowerShell7StableVersion);

        releases.Result = new GitHubFetchResult(GitHubFetchStatus.Ok, "v7.6.8", null);
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("7.6.8", settings.CachedPowerShell7StableVersion);
        Assert.Equal(now, settings.LastPowerShellReleaseCheckUtc);
        Assert.Equal("7.6.8", viewModel.LatestText);
        Assert.Equal("Not installed", viewModel.InstalledText);
    }

    [Fact]
    public void MacDeletion_UsesMacOsNoFollowFlagsRatherThanLinuxFlags()
    {
        Assert.Equal(0x0100, MacTranscriptDeletion.NoFollowFlag);
        Assert.NotEqual(0x20000, MacTranscriptDeletion.NoFollowFlag);
        Assert.Equal(0x0020, MacTranscriptDeletion.SymlinkNoFollow);
        Assert.Equal(62, MacTranscriptDeletion.ErrorLoop);
        Assert.Equal(144, MacTranscriptDeletion.StatSize);
        Assert.Equal(0, MacTranscriptDeletion.DeviceOffset);
        Assert.Equal(4, MacTranscriptDeletion.ModeOffset);
        Assert.Equal(8, MacTranscriptDeletion.InodeOffset);
        Assert.Equal("fstat", MacTranscriptDeletion.FileStatEntryPoint);
        Assert.Equal("fstatat", MacTranscriptDeletion.FileStatAtEntryPoint);
    }

    [Fact]
    public void MacDeletion_DeletesAFinishedTranscriptOnlyWhenHostedOnMacOs()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var library = NativeLibrary.Load("libSystem.dylib");
        Assert.True(NativeLibrary.TryGetExport(library, MacTranscriptDeletion.FileStatEntryPoint, out _));
        Assert.True(NativeLibrary.TryGetExport(library, MacTranscriptDeletion.FileStatAtEntryPoint, out _));

        using var directory = new TempDirectory();
        var finished = Path.GetFullPath(Path.Combine(directory.Path, "done.txt"));
        var unfinished = Path.GetFullPath(Path.Combine(directory.Path, "open.txt"));
        var neighbor = Path.Combine(directory.Path, "unrelated.txt");
        var linked = Path.GetFullPath(Path.Combine(directory.Path, "link.txt"));
        var folder = Path.GetFullPath(Path.Combine(directory.Path, "folder"));
        const string footer = "PowerShell transcript start\nPowerShell transcript end\nEnd time: 20261019030738\n";
        File.WriteAllText(finished, footer);
        File.WriteAllText(unfinished, "PowerShell transcript start\n");
        File.WriteAllText(neighbor, "keep");
        Directory.CreateDirectory(folder);
        File.CreateSymbolicLink(linked, finished);

        Assert.Equal(TranscriptDeleteStatus.NotFinished, TranscriptFileDeleter.TryDelete(unfinished).Status);
        Assert.True(File.Exists(unfinished));
        Assert.Equal(TranscriptDeleteStatus.Unsafe, TranscriptFileDeleter.TryDelete(linked).Status);
        Assert.True(File.Exists(finished));
        Assert.Equal(TranscriptDeleteStatus.Unsafe, TranscriptFileDeleter.TryDelete(folder).Status);
        Assert.True(Directory.Exists(folder));
        Assert.Equal(TranscriptDeleteStatus.Deleted, TranscriptFileDeleter.TryDelete(finished).Status);
        Assert.False(File.Exists(finished));
        Assert.Equal("keep", File.ReadAllText(neighbor));
    }

    private static EngineFileInspection Inspect(
        string path,
        IReadOnlySet<string> regular,
        IReadOnlyDictionary<string, string> links)
    {
        if (links.ContainsKey(path))
            return new EngineFileInspection(true, true);

        return new EngineFileInspection(regular.Contains(path), false);
    }

    private static PowerShellProductVersion Parsed(string text)
    {
        Assert.True(PowerShellProductVersion.TryParse(text, out var version));
        return version;
    }

    private static PowerShellVersionStatusViewModel CreateVersions(
        JsonSettingsStore store,
        ApplicationSettings settings,
        IReadOnlyList<PowerShellInstallInspection> installed,
        FakePowerShellReleases releases,
        Func<DateTimeOffset>? clock = null)
    {
        var localization = new LocalizationService(LocalizationDirectory());
        localization.SetLanguage("en-US");
        return new PowerShellVersionStatusViewModel(
            localization,
            settings,
            store,
            new FakePowerShellInspector(installed),
            releases,
            new InlineUiInvoker(),
            clock ?? (() => DateTimeOffset.UtcNow));
    }

    private static string LocalizationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src/PowerShellSessionRecorder.App/Localization");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate localization files.");
    }

    private sealed class FakePowerShellInspector(IReadOnlyList<PowerShellInstallInspection> installed) : IInstalledPowerShellInspector
    {
        public IReadOnlyList<PowerShellInstallInspection> Inspect() => installed;
    }

    private sealed class FakePowerShellReleases : IPowerShellReleaseLookup
    {
        public int Calls { get; private set; }

        public GitHubFetchResult Result { get; set; }

        public Task<GitHubFetchResult> GetLatestStableAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
