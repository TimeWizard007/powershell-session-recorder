namespace PowerShellSessionRecorder.Core.Updates;

public enum GitHubFetchStatus
{
    Ok,
    NotFound,
    RateLimited,
    Offline,
    TimedOut,
    Canceled,
    Malformed,
    MissingTag
}

public readonly record struct GitHubFetchResult(GitHubFetchStatus Status, string? TagName, string? HtmlUrl);

public enum UpdateCheckKind
{
    UpdateAvailable,
    UpToDate,
    IgnoredPrerelease,
    NoPublishedRelease,
    InvalidRelease,
    Offline,
    TimedOut,
    Canceled,
    RateLimited,
    MalformedResponse,
    NotDue
}

public sealed record UpdateCheckOutcome(
    UpdateCheckKind Kind,
    string CurrentVersion,
    string? LatestStableVersion,
    string? ReleasePageUrl)
{
    public bool CountsAsSuccessfulCheck =>
        Kind is UpdateCheckKind.UpdateAvailable
            or UpdateCheckKind.UpToDate
            or UpdateCheckKind.IgnoredPrerelease
            or UpdateCheckKind.NoPublishedRelease;

    public static UpdateCheckOutcome Interpret(string currentVersion, GitHubFetchResult fetch)
        => Interpret(currentVersion, fetch, UpdateChannel.Stable);

    public static UpdateCheckOutcome Interpret(string currentVersion, GitHubFetchResult fetch, UpdateChannel channel)
    {
        currentVersion ??= "";
        switch (fetch.Status)
        {
            case GitHubFetchStatus.NotFound:
                return new UpdateCheckOutcome(UpdateCheckKind.NoPublishedRelease, currentVersion, null, null);
            case GitHubFetchStatus.RateLimited:
                return new UpdateCheckOutcome(UpdateCheckKind.RateLimited, currentVersion, null, null);
            case GitHubFetchStatus.Offline:
                return new UpdateCheckOutcome(UpdateCheckKind.Offline, currentVersion, null, null);
            case GitHubFetchStatus.TimedOut:
                return new UpdateCheckOutcome(UpdateCheckKind.TimedOut, currentVersion, null, null);
            case GitHubFetchStatus.Canceled:
                return new UpdateCheckOutcome(UpdateCheckKind.Canceled, currentVersion, null, null);
            case GitHubFetchStatus.Malformed:
                return new UpdateCheckOutcome(UpdateCheckKind.MalformedResponse, currentVersion, null, null);
            case GitHubFetchStatus.MissingTag:
                return new UpdateCheckOutcome(UpdateCheckKind.InvalidRelease, currentVersion, null, null);
            case GitHubFetchStatus.Ok:
                break;
            default:
                return new UpdateCheckOutcome(UpdateCheckKind.MalformedResponse, currentVersion, null, null);
        }

        if (!ReleaseVersion.TryParse(currentVersion, out var local)
            || !ReleaseVersion.TryParse(fetch.TagName, out var remote))
        {
            return new UpdateCheckOutcome(UpdateCheckKind.InvalidRelease, currentVersion, null, null);
        }

        if (channel == UpdateChannel.Stable && !remote.IsStable)
        {
            return new UpdateCheckOutcome(UpdateCheckKind.IgnoredPrerelease, currentVersion, null, null);
        }

        var page = ReleasePageLink.SelectTrustedPage(fetch.HtmlUrl, fetch.TagName);
        if (remote.CompareTo(local) > 0)
        {
            return new UpdateCheckOutcome(
                UpdateCheckKind.UpdateAvailable,
                currentVersion,
                fetch.TagName,
                page);
        }

        return new UpdateCheckOutcome(UpdateCheckKind.UpToDate, currentVersion, fetch.TagName, page);
    }

    public static void Remember(
        ApplicationSettings settings,
        UpdateCheckOutcome outcome,
        DateTimeOffset utcNow,
        UpdateChannel channel)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(outcome);
        if (!outcome.CountsAsSuccessfulCheck || settings.UpdateChannel != channel)
            return;

        settings.LastUpdateCheckUtc = utcNow;
        settings.CachedUpdateChannel = channel;
        switch (outcome.Kind)
        {
            case UpdateCheckKind.UpdateAvailable:
            case UpdateCheckKind.UpToDate:
                settings.LastKnownStableVersion = outcome.LatestStableVersion ?? "";
                settings.LastKnownReleaseUrl = outcome.ReleasePageUrl ?? "";
                break;
            case UpdateCheckKind.NoPublishedRelease:
            case UpdateCheckKind.IgnoredPrerelease:
                settings.LastKnownStableVersion = "";
                settings.LastKnownReleaseUrl = "";
                break;
        }
    }
}
