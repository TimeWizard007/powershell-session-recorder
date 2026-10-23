using System.Net;
using System.Net.Http.Headers;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.Services.Updates;

public interface IGitHubReleaseSource
{
    Task<GitHubFetchResult> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken);
}

/// <summary>
/// Reads published GitHub releases. The request carries no local configuration.
/// Stable uses the latest-release endpoint. Beta reads a bounded number of release pages.
/// </summary>
public sealed class GitHubReleaseClient : IGitHubReleaseSource
{
    public static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases/latest");

    public static readonly Uri BetaReleasesUri = new(
        "https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases?per_page=100&page=1");

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    private readonly HttpClient _http;

    public GitHubReleaseClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static HttpClient CreateClient(string applicationVersion)
    {
        var version = string.IsNullOrWhiteSpace(applicationVersion) ? "0" : applicationVersion.Trim();
        var client = new HttpClient { Timeout = DefaultTimeout };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            $"PowerShellSessionRecorder/{version} (+https://github.com/TimeWizard007/powershell-session-recorder)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public Task<GitHubFetchResult> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken)
        => channel == UpdateChannel.Beta
            ? GetBetaAsync(cancellationToken)
            : GetStableAsync(cancellationToken);

    private async Task<GitHubFetchResult> GetStableAsync(CancellationToken cancellationToken)
    {
        var page = await ReadPageAsync(LatestReleaseUri, list: false, cancellationToken).ConfigureAwait(false);
        if (page.Failure is { } failure)
            return failure;

        var document = page.Document;
        if (document.Draft)
            return new GitHubFetchResult(GitHubFetchStatus.NotFound, null, null);

        return document.Status switch
        {
            GitHubDocumentStatus.Parsed => new GitHubFetchResult(GitHubFetchStatus.Ok, document.TagName, document.HtmlUrl),
            GitHubDocumentStatus.MissingTag => new GitHubFetchResult(GitHubFetchStatus.MissingTag, null, null),
            _ => new GitHubFetchResult(GitHubFetchStatus.Malformed, null, null)
        };
    }

    private async Task<GitHubFetchResult> GetBetaAsync(CancellationToken cancellationToken)
    {
        var collected = new List<PublishedRelease>();
        var next = BetaReleasesUri;
        for (var pageNumber = 1; pageNumber <= ReleaseCatalog.MaximumPages; pageNumber++)
        {
            var page = await ReadPageAsync(next, list: true, cancellationToken).ConfigureAwait(false);
            if (page.Failure is { } failure)
                return failure;

            collected.AddRange(page.Releases);
            if (page.Next is null || pageNumber == ReleaseCatalog.MaximumPages)
                break;

            if (!IsAllowedBetaPage(page.Next, pageNumber + 1))
                break;

            next = page.Next;
        }

        return ReleaseCatalog.Choose(UpdateChannel.Beta, collected);
    }

    private async Task<PageRead> ReadPageAsync(Uri uri, bool list, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return PageRead.Failed(GitHubFetchStatus.NotFound);

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return PageRead.Failed(GitHubFetchStatus.RateLimited);

            if (!response.IsSuccessStatusCode)
                return PageRead.Failed(GitHubFetchStatus.Malformed);

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!list)
            {
                return new PageRead(GitHubReleaseDocument.Parse(json), [], null, null);
            }

            if (!GitHubReleaseDocument.TryParseList(json, out var releases))
                return PageRead.Failed(GitHubFetchStatus.Malformed);

            return new PageRead(default, releases, NextLink(response), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PageRead.Failed(GitHubFetchStatus.Canceled);
        }
        catch (OperationCanceledException)
        {
            return PageRead.Failed(GitHubFetchStatus.TimedOut);
        }
        catch (HttpRequestException)
        {
            return PageRead.Failed(GitHubFetchStatus.Offline);
        }
    }

    internal static bool IsAllowedBetaPage(Uri uri, int pageNumber)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.Port is not (-1 or 443))
            return false;

        if (!string.Equals(uri.AbsolutePath, "/repos/TimeWizard007/powershell-session-recorder/releases", StringComparison.Ordinal))
            return false;

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var perPage = false;
        var page = false;
        foreach (var item in query)
        {
            var parts = item.Split('=', 2);
            if (parts.Length != 2)
                return false;

            if (parts[0] == "per_page" && parts[1] == ReleaseCatalog.PageSize.ToString())
                perPage = true;
            else if (parts[0] == "page" && parts[1] == pageNumber.ToString())
                page = true;
            else
                return false;
        }

        return perPage && page;
    }

    private static Uri? NextLink(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
            return null;

        foreach (var value in values)
        {
            foreach (var part in value.Split(','))
            {
                var pieces = part.Split(';', 2);
                if (pieces.Length != 2 || !pieces[1].Contains("rel=\"next\"", StringComparison.Ordinal))
                    continue;

                var link = pieces[0].Trim();
                if (link.Length < 2 || link[0] != '<' || link[^1] != '>')
                    continue;

                if (Uri.TryCreate(link[1..^1], UriKind.Absolute, out var uri))
                    return uri;
            }
        }

        return null;
    }

    private readonly record struct PageRead(
        GitHubReleaseDocument Document,
        IReadOnlyList<PublishedRelease> Releases,
        Uri? Next,
        GitHubFetchResult? Failure)
    {
        public static PageRead Failed(GitHubFetchStatus status)
            => new(default, [], null, new GitHubFetchResult(status, null, null));
    }
}
