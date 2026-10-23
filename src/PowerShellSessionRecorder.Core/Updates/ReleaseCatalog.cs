namespace PowerShellSessionRecorder.Core.Updates;

public readonly record struct PublishedRelease(string? TagName, string? HtmlUrl, bool Draft, bool Prerelease);

/// <summary>
/// Picks the newest published release for a channel.
/// The first list entry is not assumed to be the newest version.
/// </summary>
public static class ReleaseCatalog
{
    public const int MaximumPages = 3;

    public const int PageSize = 100;

    public static GitHubFetchResult Choose(UpdateChannel channel, IReadOnlyList<PublishedRelease> releases)
    {
        ArgumentNullException.ThrowIfNull(releases);
        PublishedRelease? bestRelease = null;
        ReleaseVersion bestVersion = default;
        var sawInvalidTag = false;

        foreach (var release in releases)
        {
            if (release.Draft)
                continue;

            if (channel == UpdateChannel.Stable && release.Prerelease)
                continue;

            if (!ReleaseVersion.TryParse(release.TagName, out var version))
            {
                sawInvalidTag = true;
                continue;
            }

            if (channel == UpdateChannel.Stable && !version.IsStable)
                continue;

            if (bestRelease is not null && version.CompareTo(bestVersion) <= 0)
                continue;

            bestRelease = release;
            bestVersion = version;
        }

        if (bestRelease is null)
        {
            return new GitHubFetchResult(
                sawInvalidTag ? GitHubFetchStatus.MissingTag : GitHubFetchStatus.NotFound,
                null,
                null);
        }

        return new GitHubFetchResult(GitHubFetchStatus.Ok, bestRelease.Value.TagName, bestRelease.Value.HtmlUrl);
    }
}
