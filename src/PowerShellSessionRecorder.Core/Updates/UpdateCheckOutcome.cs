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

        var relation = ReleaseVersionComparison.Relate(currentVersion, fetch.TagName);
        var page = ReleasePageLink.SelectTrustedPage(fetch.HtmlUrl, fetch.TagName);
        return relation switch
        {
            ReleaseRelation.NewerStable => new UpdateCheckOutcome(
                UpdateCheckKind.UpdateAvailable,
                currentVersion,
                fetch.TagName,
                page),
            ReleaseRelation.SameStable or ReleaseRelation.RemoteOlder => new UpdateCheckOutcome(
                UpdateCheckKind.UpToDate,
                currentVersion,
                fetch.TagName,
                page),
            ReleaseRelation.RemotePrerelease => new UpdateCheckOutcome(
                UpdateCheckKind.IgnoredPrerelease,
                currentVersion,
                null,
                null),
            _ => new UpdateCheckOutcome(UpdateCheckKind.InvalidRelease, currentVersion, null, null)
        };
    }

    public static void Remember(ApplicationSettings settings, UpdateCheckOutcome outcome, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(outcome);
        if (!outcome.CountsAsSuccessfulCheck)
            return;

        settings.LastUpdateCheckUtc = utcNow;
        switch (outcome.Kind)
        {
            case UpdateCheckKind.UpdateAvailable:
            case UpdateCheckKind.UpToDate:
                settings.LastKnownStableVersion = outcome.LatestStableVersion ?? "";
                settings.LastKnownReleaseUrl = outcome.ReleasePageUrl ?? "";
                break;
            case UpdateCheckKind.NoPublishedRelease:
                settings.LastKnownStableVersion = "";
                settings.LastKnownReleaseUrl = "";
                break;
        }
    }
}
