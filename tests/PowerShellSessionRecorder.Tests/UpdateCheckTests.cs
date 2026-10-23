using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.Services.Updates;
using PowerShellSessionRecorder.App.ViewModels;
using PowerShellSessionRecorder.Core;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("0.1.0", "0.2.0", ReleaseRelation.NewerStable)]
    [InlineData("0.2.0", "0.2.0", ReleaseRelation.SameStable)]
    [InlineData("0.3.0", "0.2.0", ReleaseRelation.RemoteOlder)]
    [InlineData("0.1.0", "v0.2.0", ReleaseRelation.NewerStable)]
    [InlineData("0.2.0", "v0.2.0", ReleaseRelation.SameStable)]
    [InlineData("0.2.0+local", "v0.2.0", ReleaseRelation.SameStable)]
    [InlineData("0.1.0", "0.9.0-beta.1", ReleaseRelation.RemotePrerelease)]
    [InlineData("1.0.0", "v1.1.0-rc.1", ReleaseRelation.RemotePrerelease)]
    [InlineData("0.1.0-checkpoint.2.1", "0.2.0", ReleaseRelation.NewerStable)]
    [InlineData("0.1.0-checkpoint.2.1", "v0.2.0", ReleaseRelation.NewerStable)]
    [InlineData("0.1.0-beta.1", "0.2.0", ReleaseRelation.NewerStable)]
    [InlineData("0.1.0-beta.1", "0.1.0", ReleaseRelation.NewerStable)]
    [InlineData("0.1.0-beta.1", "0.1.0-beta.2", ReleaseRelation.RemotePrerelease)]
    [InlineData("0.1.0-checkpoint.2.1", "0.1.0", ReleaseRelation.NewerStable)]
    [InlineData("0.2.0-checkpoint.1", "0.1.0", ReleaseRelation.RemoteOlder)]
    [InlineData("0.1.0", "latest", ReleaseRelation.InvalidRemote)]
    [InlineData("0.1.0", "", ReleaseRelation.InvalidRemote)]
    [InlineData("not-a-version", "0.2.0", ReleaseRelation.InvalidLocal)]
    public void Relate_ComparesStableReleasesBySemanticVersion(string local, string remote, ReleaseRelation expected)
        => Assert.Equal(expected, ReleaseVersionComparison.Relate(local, remote));

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("1.0")]
    [InlineData("1.2.3-")]
    [InlineData("01.2.3")]
    public void TryParse_RejectsInvalidTags(string tag)
        => Assert.False(ReleaseVersion.TryParse(tag, out _));

    [Fact]
    public void CheckpointInformationalVersion_IsAPrereleaseOfItsCore()
    {
        Assert.True(ReleaseVersion.TryParse("0.1.0-checkpoint.2.1", out var version));
        Assert.False(version.IsStable);
        Assert.Equal(0, version.Major);
        Assert.Equal(1, version.Minor);
        Assert.Equal(0, version.Patch);
    }

    [Fact]
    public void ReleasePage_AcceptsOnlyThisRepositoriesHttpsRelease()
    {
        Assert.True(ReleasePageLink.IsTrusted(
            "https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/v0.2.0"));
        Assert.False(ReleasePageLink.IsTrusted("http://github.com/TimeWizard007/powershell-session-recorder/releases/tag/v0.2.0"));
        Assert.False(ReleasePageLink.IsTrusted("https://github.com.evil.example/TimeWizard007/powershell-session-recorder/releases/tag/v0.2.0"));
        Assert.False(ReleasePageLink.IsTrusted("https://example.com/release"));
        Assert.Equal(
            "https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/v0.2.0",
            ReleasePageLink.SelectTrustedPage("https://evil.example/setup", "v0.2.0"));
    }

    [Fact]
    public void Interpret_NewerStableRelease_DoesNotKeepAssetUrls()
    {
        var outcome = UpdateCheckOutcome.Interpret(
            "0.1.0-checkpoint.2.2",
            new GitHubFetchResult(
                GitHubFetchStatus.Ok,
                "v0.2.0",
                "https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/v0.2.0"));

        Assert.Equal(UpdateCheckKind.UpdateAvailable, outcome.Kind);
        Assert.Equal("0.1.0-checkpoint.2.2", outcome.CurrentVersion);
        Assert.Equal("v0.2.0", outcome.LatestStableVersion);
        Assert.DoesNotContain("browser_download_url", outcome.ReleasePageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void BetaProductVersion_IsReportedToGitHubAndIsNotAStableRelease()
    {
        Assert.Equal("0.1.0-beta.2", ProductVersion.Current);
        Assert.Equal(ReleaseRelation.RemotePrerelease, ReleaseVersionComparison.Relate(ProductVersion.Current, "0.1.0-beta.3"));
        using var client = GitHubReleaseClient.CreateClient(ProductVersion.Current);
        Assert.Contains(
            "PowerShellSessionRecorder/0.1.0-beta.2",
            client.DefaultRequestHeaders.UserAgent.ToString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases/latest",
            GitHubReleaseClient.LatestReleaseUri.AbsoluteUri);
    }

    [Fact]
    public void Interpret_PrereleaseIsNotAnUpdate()
    {
        var outcome = UpdateCheckOutcome.Interpret(
            "0.1.0",
            new GitHubFetchResult(GitHubFetchStatus.Ok, "0.9.0-beta.1", "https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/0.9.0-beta.1"));

        Assert.Equal(UpdateCheckKind.IgnoredPrerelease, outcome.Kind);
        Assert.Null(outcome.LatestStableVersion);
    }

    [Theory]
    [InlineData(GitHubFetchStatus.NotFound, UpdateCheckKind.NoPublishedRelease)]
    [InlineData(GitHubFetchStatus.Offline, UpdateCheckKind.Offline)]
    [InlineData(GitHubFetchStatus.TimedOut, UpdateCheckKind.TimedOut)]
    [InlineData(GitHubFetchStatus.Canceled, UpdateCheckKind.Canceled)]
    [InlineData(GitHubFetchStatus.RateLimited, UpdateCheckKind.RateLimited)]
    [InlineData(GitHubFetchStatus.Malformed, UpdateCheckKind.MalformedResponse)]
    [InlineData(GitHubFetchStatus.MissingTag, UpdateCheckKind.InvalidRelease)]
    public void Interpret_MapsFetchFailures(GitHubFetchStatus fetch, UpdateCheckKind expected)
    {
        var outcome = UpdateCheckOutcome.Interpret("0.1.0-checkpoint.2.2", new GitHubFetchResult(fetch, null, null));
        Assert.Equal(expected, outcome.Kind);
        Assert.NotEqual(UpdateCheckKind.UpdateAvailable, outcome.Kind);
    }

    [Fact]
    public void Document_MalformedJsonAndMissingTag_AreDistinct()
    {
        Assert.Equal(GitHubDocumentStatus.Malformed, GitHubReleaseDocument.Parse("{").Status);
        Assert.Equal(GitHubDocumentStatus.Malformed, GitHubReleaseDocument.Parse("[]").Status);
        Assert.Equal(GitHubDocumentStatus.MissingTag, GitHubReleaseDocument.Parse("""{"assets":[]}""").Status);

        var parsed = GitHubReleaseDocument.Parse("""
            {"tag_name":"v0.2.0","html_url":"https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/v0.2.0","body":"HOWE secret"}
            """);
        Assert.Equal(GitHubDocumentStatus.Parsed, parsed.Status);
        Assert.Equal("v0.2.0", parsed.TagName);
        Assert.DoesNotContain("HOWE", parsed.HtmlUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_RequestsOnlyTheOfficialReleaseEndpoint()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"tag_name":"v0.2.0","html_url":"https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/v0.2.0","assets":[{"browser_download_url":"https://example.invalid/setup.exe"}]}
                    """)
            }
        };
        using var http = GitHubReleaseClient.CreateClient("0.1.0-checkpoint.2.2");
        http.Timeout = TimeSpan.FromSeconds(5);
        // Replace the handler by constructing a client around the recording handler while keeping the headers.
        using var sending = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        foreach (var header in http.DefaultRequestHeaders)
            sending.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

        var result = await new GitHubReleaseClient(sending).GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);

        Assert.Equal(GitHubReleaseClient.LatestReleaseUri, handler.Request!.RequestUri);
        Assert.Equal(HttpMethod.Get, handler.Request.Method);
        Assert.Null(handler.Request.Content);
        Assert.Contains("PowerShellSessionRecorder/0.1.0-checkpoint.2.2", handler.Request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
        Assert.Equal("", handler.Request.RequestUri!.Query);
        Assert.DoesNotContain("HOWE", handler.Request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("v0.2.0", result.TagName);
        Assert.DoesNotContain("setup.exe", result.HtmlUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_NotFoundTimeoutCancellationRateLimitAndMalformed_AreDistinct()
    {
        Assert.Equal(GitHubFetchStatus.NotFound, (await Send(new HttpResponseMessage(HttpStatusCode.NotFound))).Status);
        Assert.Equal(GitHubFetchStatus.RateLimited, (await Send(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("HOWE")
        })).Status);
        Assert.Equal(GitHubFetchStatus.Malformed, (await Send(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{")
        })).Status);

        using var timingOut = new HttpClient(new DelayHandler()) { Timeout = TimeSpan.FromMilliseconds(100) };
        var timedOut = await new GitHubReleaseClient(timingOut).GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);
        Assert.Equal(GitHubFetchStatus.TimedOut, timedOut.Status);

        using var cancelClient = new HttpClient(new DelayHandler()) { Timeout = TimeSpan.FromSeconds(5) };
        using var cancel = new CancellationTokenSource();
        var pending = new GitHubReleaseClient(cancelClient).GetLatestAsync(UpdateChannel.Stable, cancel.Token);
        cancel.Cancel();
        var canceled = await pending;
        Assert.Equal(GitHubFetchStatus.Canceled, canceled.Status);

        using var offlineClient = new HttpClient(new ThrowingHandler()) { Timeout = TimeSpan.FromSeconds(5) };
        var offline = await new GitHubReleaseClient(offlineClient).GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);
        Assert.Equal(GitHubFetchStatus.Offline, offline.Status);
    }

    [Fact]
    public async Task AutomaticCheck_IsLimitedToOncePerDayAcrossRestarts_AndManualCheckBypassesIt()
    {
        var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var settings = new ApplicationSettings
        {
            CheckForUpdatesAutomatically = true,
            LastUpdateCheckUtc = now.AddHours(-23),
            LastKnownStableVersion = "v0.2.0"
        };
        var source = new FakeSource(new GitHubFetchResult(GitHubFetchStatus.Ok, "v0.3.0", ReleasePage("v0.3.0")));
        var first = new UpdateCheckCoordinator(source, () => now, "0.1.0");

        var throttled = await first.CheckAsync(settings, manual: false, CancellationToken.None);

        Assert.Equal(UpdateCheckKind.NotDue, throttled.Kind);
        Assert.Equal(0, source.Calls);
        Assert.Equal("v0.2.0", throttled.LatestStableVersion);

        var restarted = new UpdateCheckCoordinator(source, () => now, "0.1.0");
        var stillThrottled = await restarted.CheckAsync(settings, manual: false, CancellationToken.None);
        Assert.Equal(UpdateCheckKind.NotDue, stillThrottled.Kind);
        Assert.Equal(0, source.Calls);

        var manual = await restarted.CheckAsync(settings, manual: true, CancellationToken.None);
        Assert.Equal(UpdateCheckKind.UpdateAvailable, manual.Kind);
        Assert.Equal(1, source.Calls);
        Assert.Equal(now, settings.LastUpdateCheckUtc);
    }

    [Fact]
    public async Task AutomaticCheck_RunsWhenTheLastSuccessIsAtLeastADayOld()
    {
        var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var settings = new ApplicationSettings { LastUpdateCheckUtc = now.AddHours(-24) };
        var source = new FakeSource(new GitHubFetchResult(GitHubFetchStatus.NotFound, null, null));
        var coordinator = new UpdateCheckCoordinator(source, () => now, "0.1.0-checkpoint.2.2");

        var outcome = await coordinator.CheckAsync(settings, manual: false, CancellationToken.None);

        Assert.Equal(UpdateCheckKind.NoPublishedRelease, outcome.Kind);
        Assert.Equal(1, source.Calls);
        Assert.Equal("", settings.LastKnownStableVersion);
        Assert.Equal(now, settings.LastUpdateCheckUtc);
    }

    [Fact]
    public async Task PreferenceAndDismissal_AreIndependentOfRecording()
    {
        using var directory = new TempDirectory();
        var settings = new ApplicationSettings { CheckForUpdatesAutomatically = true };
        var store = new JsonSettingsStore(Path.Combine(directory.Path, "settings.json"));
        var localization = new LocalizationService(LocalizationDirectory());
        var source = new FakeSource(new GitHubFetchResult(GitHubFetchStatus.Ok, "v0.2.0", ReleasePage("v0.2.0")));
        var coordinator = new UpdateCheckCoordinator(source, () => DateTimeOffset.Parse("2026-10-18T12:00:00Z"), "0.1.0-checkpoint.2.2");
        var updates = new UpdateStatusViewModel(localization, settings, store, coordinator, links: null, _ => { });
        var recording = new SessionSetupRecordingViewModelTests.FakeRecordingService();
        var session = new SessionSetupViewModel(
            localization,
            settings,
            store,
            new ObservableCollection<string>(),
            _ => { },
            recording);
        session.CustomerTag = "HOWE";
        session.SessionName = "Diagnostyka VPN";
        session.LogDirectory = directory.Path;

        updates.CheckForUpdatesAutomatically = false;
        Assert.False(store.Load().CheckForUpdatesAutomatically);
        updates.BeginAutomaticCheck(CancellationToken.None);
        Assert.Equal(0, source.Calls);

        updates.CheckForUpdatesAutomatically = true;
        await updates.CheckForUpdatesNowCommand.ExecuteAsync(null);
        Assert.True(updates.ShowNotice);
        Assert.Contains("0.1.0-checkpoint.2.2", updates.UpdateNoticeBody, StringComparison.Ordinal);
        Assert.Contains("v0.2.0", updates.UpdateNoticeBody, StringComparison.Ordinal);
        Assert.Equal("No published releases yet", localization.Get(L.UpdatesNoPublishedReleases));

        updates.DismissUpdateCommand.Execute(null);
        Assert.False(updates.ShowNotice);
        await updates.CheckForUpdatesNowCommand.ExecuteAsync(null);
        Assert.False(updates.ShowNotice);

        source.Result = new GitHubFetchResult(GitHubFetchStatus.NotFound, null, null);
        await updates.CheckForUpdatesNowCommand.ExecuteAsync(null);
        Assert.Equal("No published releases yet", updates.UpdateCheckMessage);
        localization.SetLanguage("pl-PL");
        Assert.Equal("Nie opublikowano jeszcze żadnych wydań", updates.UpdateCheckMessage);

        source.Result = new GitHubFetchResult(GitHubFetchStatus.Offline, null, null);
        await updates.CheckForUpdatesNowCommand.ExecuteAsync(null);
        Assert.Contains("GitHub", updates.UpdateCheckMessage, StringComparison.Ordinal);
        Assert.Equal(0, recording.Calls);
        Assert.True(session.CanStartSession);
        await session.StartSessionCommand.ExecuteAsync(null);
        Assert.Equal(1, recording.Calls);
    }

    [Fact]
    public async Task ManualChecks_DoNotOverlap()
    {
        using var directory = new TempDirectory();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new FakeSource(async _ =>
        {
            started.TrySetResult();
            await release.Task;
            return new GitHubFetchResult(GitHubFetchStatus.NotFound, null, null);
        });
        var updates = CreateUpdates(source, directory.Path);

        var first = updates.CheckForUpdatesNowCommand.ExecuteAsync(null);
        await started.Task;
        var second = updates.CheckForUpdatesNowCommand.ExecuteAsync(null);
        await second;
        Assert.Equal(1, source.Calls);
        release.TrySetResult();
        await first;
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public void OlderSettingsFile_KeepsAutomaticChecksEnabledWithoutRewritingTheFile()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "language": "pl-PL",
              "lastLogDirectory": "/tmp",
              "lastPowerShellVersion": "PowerShell7",
              "includeDateAndTime": false,
              "saveSessionInHistory": true,
              "recentCustomerTags": [ "HOWE" ]
            }
            """);
        var before = File.ReadAllBytes(path);

        var loaded = new JsonSettingsStore(path).LoadDetailed();

        Assert.True(loaded.Settings.CheckForUpdatesAutomatically);
        Assert.Equal(UpdateChannel.Stable, loaded.Settings.UpdateChannel);
        Assert.Null(loaded.Settings.LastUpdateCheckUtc);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Normalize_DropsAnUntrustedReleaseUrl()
    {
        var settings = new ApplicationSettings
        {
            LastKnownReleaseUrl = "https://evil.example/download"
        };

        Assert.True(settings.Normalize());
        Assert.Equal("", settings.LastKnownReleaseUrl);
    }

    private static async Task<GitHubFetchResult> Send(HttpResponseMessage response)
    {
        using var http = new HttpClient(new RecordingHandler { Response = response }) { Timeout = TimeSpan.FromSeconds(5) };
        return await new GitHubReleaseClient(http).GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);
    }

    private static UpdateStatusViewModel CreateUpdates(IGitHubReleaseSource source, string directory)
    {
        var settings = new ApplicationSettings();
        var store = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
        var coordinator = new UpdateCheckCoordinator(source, () => DateTimeOffset.UnixEpoch, "0.1.0");
        return new UpdateStatusViewModel(
            new LocalizationService(LocalizationDirectory()),
            settings,
            store,
            coordinator,
            links: null,
            _ => { });
    }

    private static string ReleasePage(string tag)
        => $"https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/{tag}";

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

        throw new InvalidOperationException("Could not locate the localization catalogs.");
    }

    private sealed class FakeSource : IGitHubReleaseSource
    {
        private readonly Func<CancellationToken, Task<GitHubFetchResult>>? _next;

        public FakeSource(GitHubFetchResult result) => Result = result;

        public FakeSource(Func<CancellationToken, Task<GitHubFetchResult>> next) => _next = next;

        public GitHubFetchResult Result { get; set; }

        public int Calls { get; private set; }

        public Task<GitHubFetchResult> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken)
        {
            Calls++;
            return _next?.Invoke(cancellationToken) ?? Task.FromResult(Result);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public HttpResponseMessage? Response { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private sealed class DelayHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The delay should have been canceled.");
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("offline");
    }
}
