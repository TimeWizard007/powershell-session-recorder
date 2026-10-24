namespace PowerShellSessionRecorder.Core.Updates;

/// <summary>
/// Selects the newest stable PowerShell 7 release. Windows PowerShell 5.1 is never compared with it.
/// </summary>
public static class PowerShellReleaseCatalog
{
    public static bool TrySelectStable(GitHubFetchResult fetch, out string version)
    {
        version = "";
        if (fetch.Status != GitHubFetchStatus.Ok)
            return false;

        if (!ReleaseVersion.TryParse(fetch.TagName, out var parsed) || !parsed.IsStable || parsed.Major < 7)
            return false;

        version = parsed.Major + "." + parsed.Minor + "." + parsed.Patch;
        return true;
    }

    public static bool TrySelectHighestStable(IReadOnlyList<PublishedRelease> releases, out string version)
    {
        ArgumentNullException.ThrowIfNull(releases);
        version = "";
        ReleaseVersion? best = null;
        foreach (var release in releases)
        {
            if (release.Draft || release.Prerelease)
                continue;

            var candidate = new GitHubFetchResult(GitHubFetchStatus.Ok, release.TagName, release.HtmlUrl);
            if (!TrySelectStable(candidate, out var text) || !ReleaseVersion.TryParse(text, out var parsed))
                continue;

            if (best is not null && parsed.CompareTo(best.Value) <= 0)
                continue;

            best = parsed;
            version = text;
        }

        return best is not null;
    }
}
