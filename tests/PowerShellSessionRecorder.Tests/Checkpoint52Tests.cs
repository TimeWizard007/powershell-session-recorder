using System.Diagnostics;
using System.Net;
using System.Net.Http;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services.Updates;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint52Tests
{
    [Theory]
    [InlineData("0.1.0-beta.2", "0.1.0-beta.3", 1)]
    [InlineData("0.1.0-beta.9", "0.1.0", 1)]
    [InlineData("0.1.0", "0.2.0-beta.1", 1)]
    [InlineData("0.2.0-beta.1", "0.1.0", -1)]
    public void SemVer_OrdersStableAndPrerelease(string left, string right, int expectedSign)
    {
        Assert.True(ReleaseVersion.TryParse(left, out var olderOrEqual));
        Assert.True(ReleaseVersion.TryParse(right, out var candidate));
        Assert.Equal(expectedSign, Math.Sign(candidate.CompareTo(olderOrEqual)));
    }

    [Fact]
    public void BetaChannel_PicksTheNewestPublishedReleaseAndSkipsDraftsAndInvalidTags()
    {
        var selected = ReleaseCatalog.Choose(UpdateChannel.Beta,
        [
            new PublishedRelease("not-a-version", null, false, false),
            new PublishedRelease("0.1.0-beta.9", Page("0.1.0-beta.9"), true, true),
            new PublishedRelease("0.1.0-beta.2", Page("0.1.0-beta.2"), false, true),
            new PublishedRelease("0.1.0-beta.3", Page("0.1.0-beta.3"), false, true),
            new PublishedRelease("0.1.0", Page("0.1.0"), false, false)
        ]);

        Assert.Equal(GitHubFetchStatus.Ok, selected.Status);
        Assert.Equal("0.1.0", selected.TagName);

        var onlyInvalid = ReleaseCatalog.Choose(UpdateChannel.Beta, [new PublishedRelease("latest", null, false, false)]);
        Assert.Equal(GitHubFetchStatus.MissingTag, onlyInvalid.Status);

        var onlyDrafts = ReleaseCatalog.Choose(UpdateChannel.Beta, [new PublishedRelease("9.0.0", null, true, false)]);
        Assert.Equal(GitHubFetchStatus.NotFound, onlyDrafts.Status);
    }

    [Fact]
    public void StableChannel_IgnoresPrereleasesAndDoesNotOfferAnOlderVersion()
    {
        var stable = ReleaseCatalog.Choose(UpdateChannel.Stable,
        [
            new PublishedRelease("0.9.0-beta.1", Page("0.9.0-beta.1"), false, true),
            new PublishedRelease("0.2.0", Page("0.2.0"), false, false)
        ]);
        Assert.Equal("0.2.0", stable.TagName);

        var older = UpdateCheckOutcome.Interpret(
            "0.2.0-beta.1",
            new GitHubFetchResult(GitHubFetchStatus.Ok, "0.1.0", Page("0.1.0")),
            UpdateChannel.Beta);
        Assert.Equal(UpdateCheckKind.UpToDate, older.Kind);

        var newerBeta = UpdateCheckOutcome.Interpret(
            "0.1.0-beta.2",
            new GitHubFetchResult(GitHubFetchStatus.Ok, "0.1.0-beta.3", Page("0.1.0-beta.3")),
            UpdateChannel.Beta);
        Assert.Equal(UpdateCheckKind.UpdateAvailable, newerBeta.Kind);
        Assert.Equal(Page("0.1.0-beta.3"), newerBeta.ReleasePageUrl);

        var ignored = UpdateCheckOutcome.Interpret(
            "0.1.0-beta.2",
            new GitHubFetchResult(GitHubFetchStatus.Ok, "0.1.0-beta.3", Page("0.1.0-beta.3")));
        Assert.Equal(UpdateCheckKind.IgnoredPrerelease, ignored.Kind);
    }

    [Fact]
    public void OlderSettings_DefaultToStableWithoutRewritingOrTouchingCustomers()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        var customersPath = Path.Combine(directory.Path, "customers.json");
        File.WriteAllText(settingsPath, """
            {
              "schemaVersion": 1,
              "language": "pl-PL",
              "lastLogDirectory": "/tmp/sessions",
              "saveSessionInHistory": false,
              "recentCustomerTags": [ "Gdańsk" ]
            }
            """);
        File.WriteAllText(customersPath, """{"schemaVersion":1,"customers":[]}""");
        var settingsBefore = File.ReadAllBytes(settingsPath);
        var customersBefore = File.ReadAllBytes(customersPath);

        var loaded = new JsonSettingsStore(settingsPath).Load();

        Assert.Equal(UpdateChannel.Stable, loaded.UpdateChannel);
        Assert.Equal(1, loaded.SchemaVersion);
        Assert.Equal("pl-PL", loaded.Language);
        Assert.False(loaded.SaveSessionInHistory);
        Assert.Equal(["Gdańsk"], loaded.RecentCustomerTags);
        Assert.Equal(settingsBefore, File.ReadAllBytes(settingsPath));
        Assert.Equal(customersBefore, File.ReadAllBytes(customersPath));
    }

    [Fact]
    public async Task ChannelSwitch_ClearsTheCachedStableResult()
    {
        using var directory = new TempDirectory();
        var settings = new ApplicationSettings
        {
            LastKnownStableVersion = "v0.2.0",
            LastKnownReleaseUrl = Page("v0.2.0"),
            LastUpdateCheckUtc = DateTimeOffset.UtcNow,
            CachedUpdateChannel = UpdateChannel.Stable
        };
        var store = new JsonSettingsStore(Path.Combine(directory.Path, "settings.json"));
        var source = new ScriptedSource(new GitHubFetchResult(GitHubFetchStatus.Ok, "0.1.0-beta.3", Page("0.1.0-beta.3")));
        var updates = new UpdateStatusViewModel(
            new LocalizationService(LocalizationDirectory()),
            settings,
            store,
            new UpdateCheckCoordinator(source, () => DateTimeOffset.UnixEpoch, "0.1.0-beta.2"),
            links: null,
            _ => { });

        updates.BeginAutomaticCheck(CancellationToken.None);
        Assert.True(updates.ShowNotice);
        Assert.Equal(0, source.Calls);

        updates.SelectedChannel = updates.Channels.Single(option => option.Channel == UpdateChannel.Beta);

        Assert.Equal("", settings.LastKnownStableVersion);
        Assert.Equal("", settings.LastKnownReleaseUrl);
        Assert.Null(settings.LastUpdateCheckUtc);
        Assert.False(updates.ShowNotice);
        Assert.Equal(UpdateChannel.Beta, store.Load().UpdateChannel);
        Assert.Equal("None", updates.AvailableVersionText);
        await updates.CheckForUpdatesNowCommand.ExecuteAsync(null);
        Assert.Equal(1, source.Calls);
        Assert.True(updates.ShowNotice);
        Assert.Equal("0.1.0-beta.3", updates.AvailableVersionText);
        Assert.Equal(Page("0.1.0-beta.3"), store.Load().LastKnownReleaseUrl);
    }

    [Fact]
    public async Task BetaClient_FollowsOnlyTheOfficialNextPageAndStopsOnRateLimit()
    {
        var handler = new SequenceHandler();
        handler.Enqueue(
            Json("""[{"tag_name":"0.1.0-beta.1","draft":false,"prerelease":true,"html_url":"https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/0.1.0-beta.1"}]"""),
            """<https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases?per_page=100&page=2>; rel="next" """);
        handler.Enqueue(
            Json("""[{"tag_name":"0.1.0-beta.3","draft":false,"prerelease":true,"html_url":"https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/0.1.0-beta.3"},{"tag_name":"9.0.0","draft":true,"prerelease":false}]"""),
            """<https://example.invalid/releases?page=3>; rel="next" """);
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var result = await new GitHubReleaseClient(http).GetLatestAsync(UpdateChannel.Beta, CancellationToken.None);

        Assert.Equal("0.1.0-beta.3", result.TagName);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(GitHubReleaseClient.BetaReleasesUri, handler.Requests[0].RequestUri);
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.Host == "example.invalid");

        var limited = new SequenceHandler();
        limited.Enqueue(Json("""[{"tag_name":"0.1.0-beta.4","draft":false,"prerelease":true}]"""),
            """<https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases?per_page=100&page=2>; rel="next" """);
        limited.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var limitedHttp = new HttpClient(limited) { Timeout = TimeSpan.FromSeconds(5) };
        var limitedResult = await new GitHubReleaseClient(limitedHttp).GetLatestAsync(UpdateChannel.Beta, CancellationToken.None);
        Assert.Equal(GitHubFetchStatus.RateLimited, limitedResult.Status);

        using var offlineHttp = new HttpClient(new OfflineHandler()) { Timeout = TimeSpan.FromSeconds(5) };
        var offline = await new GitHubReleaseClient(offlineHttp).GetLatestAsync(UpdateChannel.Beta, CancellationToken.None);
        Assert.Equal(GitHubFetchStatus.Offline, offline.Status);
    }

    [Fact]
    public void WindowsInstaller_DefaultsToPerUserAndKeepsTheScopeOnUpgrade()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "packaging/windows/PowerShellSessionRecorder.iss"));
        Assert.Contains("AppId={{A7B3C1E2-4F58-4D1A-9C6E-2B8F0A1D5E77}", text, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequired=lowest", text, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequiredOverridesAllowed=dialog", text, StringComparison.Ordinal);
        Assert.Contains("UsePreviousPrivileges=yes", text, StringComparison.Ordinal);
        Assert.Contains("DefaultDirName={autopf}\\PowerShellSessionRecorder", text, StringComparison.Ordinal);
        Assert.Contains("OutputBaseFilename=PowerShellSessionRecorder-{#MyAppVersion}-win-x64-setup", text, StringComparison.Ordinal);
        Assert.Contains("SetupIconFile=..\\..\\assets\\branding\\app-icon.ico", text, StringComparison.Ordinal);
        Assert.Contains("IconFilename: \"{app}\\{#MyAppExeName}\"", text, StringComparison.Ordinal);
        Assert.Contains("#define MyAppVersion \"0.1.0-beta.2\"", text, StringComparison.Ordinal);
        Assert.Contains("Excludes: \"*.pdb,settings.json,customers.json,history.json\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[Registry]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PATH", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LinuxUserInstall_UpgradesAndUninstallsWithoutUserDataOrSymlinkTargets()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var home = new TempDirectory();
        var config = Path.Combine(home.Path, ".config", "powershell-session-recorder");
        Directory.CreateDirectory(config);
        var settings = Path.Combine(config, "settings.json");
        var customers = Path.Combine(config, "customers.json");
        var history = Path.Combine(config, "history.json");
        var transcript = Path.Combine(home.Path, "transcripts", "session.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        File.WriteAllText(settings, """{"schemaVersion":1,"language":"pl-PL"}""");
        File.WriteAllText(customers, """{"schemaVersion":1,"customers":[{"tag":"Gdańsk"}]}""");
        File.WriteAllText(history, """{"schemaVersion":1,"sessions":[]}""");
        File.WriteAllText(transcript, "PowerShell transcript end");
        var before = new[] { settings, customers, history, transcript }.Select(File.ReadAllBytes).ToArray();

        var payload = CreatePayload();
        try
        {
        var installed = Run(payload, "install-user.sh", home.Path);
        Assert.Contains("Installed the per-user application 0.1.0-beta.2", installed, StringComparison.Ordinal);
        var root = Path.Combine(home.Path, ".local", "opt", "powershell-session-recorder");
        Assert.True(File.Exists(Path.Combine(root, "PowerShellSessionRecorder.dll")));
        var desktop = File.ReadAllText(Path.Combine(home.Path, ".local", "share", "applications", "powershell-session-recorder-user.desktop"));
        Assert.Contains("PowerShell Session Recorder (per user)", desktop, StringComparison.Ordinal);
        Assert.Contains("X-PSR-User-Install=1", desktop, StringComparison.Ordinal);
        Assert.Contains("Icon=powershell-session-recorder-user", desktop, StringComparison.Ordinal);
        Assert.DoesNotContain("/usr/bin/powershell-session-recorder", desktop, StringComparison.Ordinal);
        var userIcon = Path.Combine(home.Path, ".local", "share", "icons", "hicolor", "256x256", "apps", "powershell-session-recorder-user.png");
        Assert.True(File.Exists(userIcon));

        var upgraded = Run(payload, "install-user.sh", home.Path);
        Assert.Contains("Upgraded the per-user application to 0.1.0-beta.2", upgraded, StringComparison.Ordinal);

        var outside = Path.Combine(home.Path, "keep.txt");
        File.WriteAllText(outside, "keep");
        Directory.CreateDirectory(Path.Combine(home.Path, "outside-dir"));
        File.WriteAllText(Path.Combine(home.Path, "outside-dir", "keep.txt"), "keep-dir");
        File.CreateSymbolicLink(Path.Combine(root, "linked-file"), outside);
        Directory.CreateSymbolicLink(Path.Combine(root, "linked-dir"), Path.Combine(home.Path, "outside-dir"));

        var removed = Run(payload, "uninstall-user.sh", home.Path);
        Assert.Contains("Settings, customers, history, and transcripts were kept", removed, StringComparison.Ordinal);
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.Equal("keep-dir", File.ReadAllText(Path.Combine(home.Path, "outside-dir", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(home.Path, ".local", "share", "applications", "powershell-session-recorder-user.desktop")));
        Assert.False(File.Exists(Path.Combine(home.Path, ".local", "share", "icons", "hicolor", "256x256", "apps", "powershell-session-recorder-user.png")));
        Assert.Equal(before[0], File.ReadAllBytes(settings));
        Assert.Equal(before[1], File.ReadAllBytes(customers));
        Assert.Equal(before[2], File.ReadAllBytes(history));
        Assert.Equal(before[3], File.ReadAllBytes(transcript));
        }
        finally
        {
            Directory.Delete(payload, recursive: true);
        }
    }

    [Fact]
    public void LinuxUserInstall_RejectsSymlinksAndUnsafeHomes()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var home = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(home.Path, ".local"));
        var linkedOpt = Path.Combine(home.Path, "opt-target");
        Directory.CreateDirectory(linkedOpt);
        Directory.CreateSymbolicLink(Path.Combine(home.Path, ".local", "opt"), linkedOpt);
        var payload = CreatePayload();
        try
        {
        var rejected = Run(payload, "install-user.sh", home.Path, expectSuccess: false);
        Assert.Contains("Refusing symbolic link", rejected, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(linkedOpt, "powershell-session-recorder", "PowerShellSessionRecorder.dll")));

        var unsafeHome = Run(payload, "install-user.sh", home.Path + "/../" + Path.GetFileName(home.Path), expectSuccess: false);
        Assert.Contains("HOME is not a safe absolute path.", unsafeHome, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(payload, recursive: true);
        }
    }

    private static string CreatePayload()
    {
        var payload = Path.Combine(Path.GetTempPath(), "psr-user-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(payload, "app"));
        Directory.CreateDirectory(Path.Combine(payload, "icons", "hicolor", "256x256", "apps"));
        File.WriteAllText(Path.Combine(payload, "app", "PowerShellSessionRecorder"), "#!/bin/sh\nexit 0\n");
        File.WriteAllText(Path.Combine(payload, "app", "PowerShellSessionRecorder.dll"), "fixture");
        File.WriteAllText(Path.Combine(payload, "icons", "hicolor", "256x256", "apps", "powershell-session-recorder.png"), "png");
        File.WriteAllText(Path.Combine(payload, "VERSION"), "0.1.0-beta.2\n");
        foreach (var name in new[] { "install-user.sh", "uninstall-user.sh", "user-install-lib.sh" })
        {
            File.Copy(Path.Combine(RepositoryRoot(), "packaging", "linux", name), Path.Combine(payload, name));
        }

        return payload;
    }

    private static string Run(string payload, string script, string home, bool expectSuccess = true)
    {
        var start = new ProcessStartInfo
        {
            FileName = "bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment["HOME"] = home;
        start.ArgumentList.Add(Path.Combine(payload, script));
        using var process = Process.Start(start);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (expectSuccess)
            Assert.True(process.ExitCode == 0, output);
        else
            Assert.NotEqual(0, process.ExitCode);
        return output;
    }

    private static string Page(string tag)
        => $"https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/{tag}";

    private static string Json(string json) => json;

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PowerShellSessionRecorder.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string LocalizationDirectory()
    {
        var candidate = Path.Combine(RepositoryRoot(), "src/PowerShellSessionRecorder.App/Localization");
        if (!Directory.Exists(candidate))
            throw new InvalidOperationException("Could not locate the localization catalogs.");
        return candidate;
    }

    private sealed class ScriptedSource : IGitHubReleaseSource
    {
        private readonly GitHubFetchResult _result;

        public ScriptedSource(GitHubFetchResult result) => _result = result;

        public int Calls { get; private set; }

        public Task<GitHubFetchResult> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(UpdateChannel.Beta, channel);
            return Task.FromResult(_result);
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(string json, string? link = null)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            if (link is not null)
                response.Headers.TryAddWithoutValidation("Link", link);
            _responses.Enqueue(response);
        }

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("offline");
    }
}
